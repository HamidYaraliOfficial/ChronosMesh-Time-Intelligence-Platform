// Package queue implements the in-memory, priority-aware job queue that
// feeds the Worker Pool. ChronosMesh's Go service is expected to process
// thousands of concurrent jobs (reminders, recurring-event materialization,
// notification fan-out), so the queue is deliberately simple and
// lock-efficient: a small set of priority buckets, each a buffered channel,
// drained by workers in strict priority order.
package queue

import (
	"context"
	"errors"
	"sync"
	"time"

	"github.com/google/uuid"
)

// Priority levels, highest first.
type Priority int

const (
	PriorityCritical Priority = iota // deadline-imminent reminders, live sync
	PriorityHigh                     // meeting reminders, booking confirmations
	PriorityNormal                   // recurring event materialization
	PriorityLow                      // analytics rollups, cleanup
	priorityCount
)

// JobType identifies which worker handler should process a job.
type JobType string

const (
	JobReminder         JobType = "reminder"
	JobRecurringEvent   JobType = "recurring_event"
	JobEmailNotify      JobType = "email_notify"
	JobPushNotify       JobType = "push_notify"
	JobCalendarSync     JobType = "calendar_sync"
	JobAvailabilityCalc JobType = "availability_calc"
	JobWebhookDispatch  JobType = "webhook_dispatch"
)

// Job is a unit of work enqueued for asynchronous processing.
type Job struct {
	ID          string
	Type        JobType
	Priority    Priority
	WorkspaceID string
	Payload     map[string]any
	EnqueuedAt  time.Time
	Attempts    int
	MaxAttempts int
}

var ErrQueueClosed = errors.New("queue: closed")
var ErrQueueFull = errors.New("queue: buffer full, backpressure engaged")

// Queue is a bounded, priority-bucketed job queue safe for concurrent
// producers and consumers.
type Queue struct {
	buckets [priorityCount]chan Job
	closed  bool
	mu      sync.RWMutex
}

// New creates a Queue with the given per-priority buffer capacity. A
// capacity of several thousand per bucket comfortably absorbs bursts (e.g.
// a workspace with thousands of reminders firing within the same minute)
// without unbounded memory growth.
func New(capacityPerBucket int) *Queue {
	q := &Queue{}
	for i := range q.buckets {
		q.buckets[i] = make(chan Job, capacityPerBucket)
	}
	return q
}

// Enqueue submits a job. Returns ErrQueueFull if the relevant priority
// bucket is saturated (the caller should apply backpressure / retry with
// backoff rather than block indefinitely).
func (q *Queue) Enqueue(job Job) error {
	q.mu.RLock()
	defer q.mu.RUnlock()
	if q.closed {
		return ErrQueueClosed
	}
	if job.ID == "" {
		job.ID = uuid.NewString()
	}
	if job.EnqueuedAt.IsZero() {
		job.EnqueuedAt = time.Now().UTC()
	}
	if job.MaxAttempts == 0 {
		job.MaxAttempts = 5
	}
	select {
	case q.buckets[job.Priority] <- job:
		return nil
	default:
		return ErrQueueFull
	}
}

// Dequeue blocks until a job is available (highest priority bucket with a
// ready job wins) or the context is cancelled.
func (q *Queue) Dequeue(ctx context.Context) (Job, error) {
	// First pass: non-blocking check of the critical bucket so latency-
	// sensitive jobs are never delayed behind a select's pseudo-random
	// branch choice among multiple ready channels.
	select {
	case job := <-q.buckets[PriorityCritical]:
		return job, nil
	default:
	}
	select {
	case job := <-q.buckets[PriorityCritical]:
		return job, nil
	case job := <-q.buckets[PriorityHigh]:
		return job, nil
	case job := <-q.buckets[PriorityNormal]:
		return job, nil
	case job := <-q.buckets[PriorityLow]:
		return job, nil
	case <-ctx.Done():
		return Job{}, ctx.Err()
	}
}

// Len returns the number of jobs currently waiting, per priority.
func (q *Queue) Len() map[Priority]int {
	return map[Priority]int{
		PriorityCritical: len(q.buckets[PriorityCritical]),
		PriorityHigh:     len(q.buckets[PriorityHigh]),
		PriorityNormal:   len(q.buckets[PriorityNormal]),
		PriorityLow:      len(q.buckets[PriorityLow]),
	}
}

// Close stops accepting new jobs. In-flight and already-buffered jobs can
// still be drained by workers until their channels are empty.
func (q *Queue) Close() {
	q.mu.Lock()
	defer q.mu.Unlock()
	if q.closed {
		return
	}
	q.closed = true
	for _, b := range q.buckets {
		close(b)
	}
}
