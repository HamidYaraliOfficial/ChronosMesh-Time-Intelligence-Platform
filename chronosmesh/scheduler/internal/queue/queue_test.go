package queue

import (
	"context"
	"testing"
	"time"
)

func TestEnqueueDequeueOrder(t *testing.T) {
	q := New(10)
	_ = q.Enqueue(Job{Type: JobReminder, Priority: PriorityLow, WorkspaceID: "ws1"})
	_ = q.Enqueue(Job{Type: JobReminder, Priority: PriorityCritical, WorkspaceID: "ws1"})

	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()

	job, err := q.Dequeue(ctx)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if job.Priority != PriorityCritical {
		t.Fatalf("expected critical priority job first, got %v", job.Priority)
	}
}

func TestQueueFullBackpressure(t *testing.T) {
	q := New(1)
	if err := q.Enqueue(Job{Type: JobReminder, Priority: PriorityNormal, WorkspaceID: "ws1"}); err != nil {
		t.Fatalf("first enqueue should succeed: %v", err)
	}
	if err := q.Enqueue(Job{Type: JobReminder, Priority: PriorityNormal, WorkspaceID: "ws1"}); err != ErrQueueFull {
		t.Fatalf("expected ErrQueueFull, got %v", err)
	}
}

func TestCloseRejectsNewJobs(t *testing.T) {
	q := New(10)
	q.Close()
	if err := q.Enqueue(Job{Type: JobReminder, Priority: PriorityNormal, WorkspaceID: "ws1"}); err != ErrQueueClosed {
		t.Fatalf("expected ErrQueueClosed, got %v", err)
	}
}

func TestDequeueBlocksUntilContextCancelled(t *testing.T) {
	q := New(10)
	ctx, cancel := context.WithTimeout(context.Background(), 50*time.Millisecond)
	defer cancel()
	if _, err := q.Dequeue(ctx); err == nil {
		t.Fatal("expected context deadline error on empty queue")
	}
}
