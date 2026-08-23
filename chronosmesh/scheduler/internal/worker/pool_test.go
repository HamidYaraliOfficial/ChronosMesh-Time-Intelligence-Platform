package worker

import (
	"context"
	"sync/atomic"
	"testing"
	"time"

	"chronosmesh/scheduler/internal/queue"
)

func TestPoolProcessesJobs(t *testing.T) {
	q := queue.New(100)
	pool := NewPool(q, 4, WithJobTimeout(time.Second))

	var handled atomic.Int32
	pool.RegisterHandler(queue.JobReminder, func(ctx context.Context, job queue.Job) error {
		handled.Add(1)
		return nil
	})

	for i := 0; i < 20; i++ {
		_ = q.Enqueue(queue.Job{Type: queue.JobReminder, Priority: queue.PriorityNormal, WorkspaceID: "ws1"})
	}

	ctx, cancel := context.WithTimeout(context.Background(), 500*time.Millisecond)
	defer cancel()
	go pool.Run(ctx)

	deadline := time.Now().Add(400 * time.Millisecond)
	for time.Now().Before(deadline) {
		if handled.Load() == 20 {
			break
		}
		time.Sleep(10 * time.Millisecond)
	}

	if handled.Load() != 20 {
		t.Fatalf("expected 20 jobs handled, got %d", handled.Load())
	}
}

func TestPoolRetriesFailedJobs(t *testing.T) {
	q := queue.New(100)
	pool := NewPool(q, 2, WithJobTimeout(time.Second))

	var attempts atomic.Int32
	pool.RegisterHandler(queue.JobReminder, func(ctx context.Context, job queue.Job) error {
		attempts.Add(1)
		if job.Attempts < 1 {
			return context.DeadlineExceeded // force a retry
		}
		return nil
	})

	_ = q.Enqueue(queue.Job{Type: queue.JobReminder, Priority: queue.PriorityNormal, WorkspaceID: "ws1", MaxAttempts: 5})

	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()
	go pool.Run(ctx)

	deadline := time.Now().Add(2500 * time.Millisecond)
	for time.Now().Before(deadline) {
		if attempts.Load() >= 2 {
			break
		}
		time.Sleep(20 * time.Millisecond)
	}

	if attempts.Load() < 2 {
		t.Fatalf("expected at least 2 attempts (1 failure + 1 retry), got %d", attempts.Load())
	}
}
