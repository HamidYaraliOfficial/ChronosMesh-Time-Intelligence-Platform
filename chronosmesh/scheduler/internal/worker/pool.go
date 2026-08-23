// Package worker implements the concurrent Worker Pool that drains
// ChronosMesh's job queue. Designed to comfortably process thousands of
// jobs (reminders, recurring-event materialization, notification fan-out)
// with bounded concurrency, per-job timeouts, and exponential-backoff
// retry so a single slow downstream call (e.g. an email provider) cannot
// stall the whole pipeline.
package worker

import (
	"context"
	"log/slog"
	"math"
	"sync"
	"sync/atomic"
	"time"

	"chronosmesh/scheduler/internal/queue"
)

// Handler processes a single job. Returning an error triggers the retry
// policy; returning nil marks the job complete.
type Handler func(ctx context.Context, job queue.Job) error

// Pool drains jobs from a Queue using a fixed number of concurrent workers.
type Pool struct {
	q            *queue.Queue
	handlers     map[queue.JobType]Handler
	concurrency  int
	jobTimeout   time.Duration
	logger       *slog.Logger
	processed    atomic.Int64
	failed       atomic.Int64
	retried      atomic.Int64
	mu           sync.RWMutex
}

type Option func(*Pool)

func WithJobTimeout(d time.Duration) Option {
	return func(p *Pool) { p.jobTimeout = d }
}

func WithLogger(l *slog.Logger) Option {
	return func(p *Pool) { p.logger = l }
}

// NewPool creates a worker pool with `concurrency` goroutines pulling from
// `q`. For thousands of lightweight jobs (e.g. reminder fan-out), a
// concurrency in the 50-200 range per instance is typical; the service can
// also be horizontally scaled behind the same durable queue.
func NewPool(q *queue.Queue, concurrency int, opts ...Option) *Pool {
	p := &Pool{
		q:           q,
		handlers:    make(map[queue.JobType]Handler),
		concurrency: concurrency,
		jobTimeout:  30 * time.Second,
		logger:      slog.Default(),
	}
	for _, opt := range opts {
		opt(p)
	}
	return p
}

// RegisterHandler wires a JobType to its processing function.
func (p *Pool) RegisterHandler(t queue.JobType, h Handler) {
	p.mu.Lock()
	defer p.mu.Unlock()
	p.handlers[t] = h
}

// Run starts `concurrency` worker goroutines and blocks until `ctx` is
// cancelled, then waits for in-flight jobs to finish (graceful shutdown).
func (p *Pool) Run(ctx context.Context) {
	var wg sync.WaitGroup
	for i := 0; i < p.concurrency; i++ {
		wg.Add(1)
		go func(workerID int) {
			defer wg.Done()
			p.workerLoop(ctx, workerID)
		}(i)
	}
	wg.Wait()
	p.logger.Info("worker pool stopped", "processed", p.processed.Load(), "failed", p.failed.Load(), "retried", p.retried.Load())
}

func (p *Pool) workerLoop(ctx context.Context, workerID int) {
	for {
		job, err := p.q.Dequeue(ctx)
		if err != nil {
			return // context cancelled or queue closed
		}
		p.process(ctx, workerID, job)
	}
}

func (p *Pool) process(ctx context.Context, workerID int, job queue.Job) {
	p.mu.RLock()
	handler, ok := p.handlers[job.Type]
	p.mu.RUnlock()
	if !ok {
		p.logger.Warn("no handler registered for job type", "type", job.Type, "job_id", job.ID)
		return
	}

	jobCtx, cancel := context.WithTimeout(ctx, p.jobTimeout)
	defer cancel()

	start := time.Now()
	err := handler(jobCtx, job)
	elapsed := time.Since(start)

	if err == nil {
		p.processed.Add(1)
		p.logger.Info("job completed", "worker", workerID, "job_id", job.ID, "type", job.Type, "elapsed_ms", elapsed.Milliseconds())
		return
	}

	job.Attempts++
	if job.Attempts >= job.MaxAttempts {
		p.failed.Add(1)
		p.logger.Error("job failed permanently", "job_id", job.ID, "type", job.Type, "attempts", job.Attempts, "error", err)
		return
	}

	p.retried.Add(1)
	backoff := time.Duration(math.Pow(2, float64(job.Attempts))) * time.Second
	p.logger.Warn("job failed, scheduling retry", "job_id", job.ID, "type", job.Type, "attempt", job.Attempts, "backoff", backoff, "error", err)

	go func(j queue.Job, delay time.Duration) {
		timer := time.NewTimer(delay)
		defer timer.Stop()
		select {
		case <-timer.C:
			if enqueueErr := p.q.Enqueue(j); enqueueErr != nil {
				p.logger.Error("failed to re-enqueue retry", "job_id", j.ID, "error", enqueueErr)
			}
		case <-ctx.Done():
		}
	}(job, backoff)
}

// Stats returns cumulative counters, primarily for the /v1/stats endpoint
// and Analytics dashboard.
func (p *Pool) Stats() (processed, failed, retried int64) {
	return p.processed.Load(), p.failed.Load(), p.retried.Load()
}
