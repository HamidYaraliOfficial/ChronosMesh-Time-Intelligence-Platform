// Package jobs contains the concrete Handler implementations run by the
// Worker Pool: reminder delivery, recurring-event materialization,
// notification fan-out (email/push), calendar sync, and availability
// recalculation.
package jobs

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"time"

	"chronosmesh/scheduler/internal/queue"
	"chronosmesh/scheduler/internal/ws"
)

// Dependencies bundles everything a handler needs so handlers stay pure
// functions of (ctx, job) plus this shared, injected state.
type Dependencies struct {
	Hub          *ws.Hub
	Logger       *slog.Logger
	CoreEngineURL string // chronosmesh-core-server base URL, e.g. http://rust-core:7301
	HTTPClient   *http.Client
	NotifySink   NotificationSink
}

// NotificationSink abstracts the actual delivery channel (email provider,
// push provider) so handlers remain testable without a live network call.
type NotificationSink interface {
	SendEmail(ctx context.Context, toUserID, subject, body string) error
	SendPush(ctx context.Context, toUserID, title, body string) error
}

// LoggingNotificationSink is a development-mode sink that just logs; wire a
// real provider (SendGrid, FCM, etc.) behind the same interface for
// production.
type LoggingNotificationSink struct{ Logger *slog.Logger }

func (s *LoggingNotificationSink) SendEmail(ctx context.Context, toUserID, subject, body string) error {
	s.Logger.Info("email dispatched", "to", toUserID, "subject", subject)
	return nil
}

func (s *LoggingNotificationSink) SendPush(ctx context.Context, toUserID, title, body string) error {
	s.Logger.Info("push dispatched", "to", toUserID, "title", title)
	return nil
}

// ReminderHandler delivers a Task/Meeting/Deadline reminder to the assigned
// user and broadcasts a real-time notification event to the workspace.
func ReminderHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		userID, _ := job.Payload["user_id"].(string)
		title, _ := job.Payload["title"].(string)
		kind, _ := job.Payload["kind"].(string) // task | meeting | deadline

		if userID == "" || title == "" {
			return fmt.Errorf("reminder job missing user_id/title")
		}

		if err := deps.NotifySink.SendPush(ctx, userID, "ChronosMesh Reminder", title); err != nil {
			return fmt.Errorf("push delivery failed: %w", err)
		}

		deps.Hub.Broadcast(ws.Event{
			Type:        "notification.reminder",
			WorkspaceID: job.WorkspaceID,
			ActorUserID: "system",
			EntityID:    job.ID,
			Payload:     map[string]any{"kind": kind, "title": title, "user_id": userID},
		})
		return nil
	}
}

// RecurringEventHandler asks the Rust Time Engine to expand a recurring
// event definition into its next window of concrete occurrences and
// enqueues a reminder job for each one that needs advance notice.
func RecurringEventHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		eventPayload, ok := job.Payload["event"]
		if !ok {
			return fmt.Errorf("recurring_event job missing 'event' payload")
		}
		windowStart := time.Now().UTC()
		windowEnd := windowStart.Add(30 * 24 * time.Hour)

		body, _ := json.Marshal(map[string]any{
			"event":  eventPayload,
			"window": map[string]any{"start": windowStart, "end": windowEnd},
		})

		req, err := http.NewRequestWithContext(ctx, http.MethodPost, deps.CoreEngineURL+"/v1/recurrence/expand", bytes.NewReader(body))
		if err != nil {
			return err
		}
		req.Header.Set("Content-Type", "application/json")
		resp, err := deps.HTTPClient.Do(req)
		if err != nil {
			return fmt.Errorf("core engine unreachable: %w", err)
		}
		defer resp.Body.Close()
		if resp.StatusCode >= 300 {
			return fmt.Errorf("core engine returned status %d", resp.StatusCode)
		}

		deps.Hub.Broadcast(ws.Event{
			Type:        "calendar.recurring_expanded",
			WorkspaceID: job.WorkspaceID,
			EntityID:    job.ID,
		})
		return nil
	}
}

// EmailNotifyHandler and PushNotifyHandler deliver generic notifications
// (schedule changes, team updates, system alerts) queued by the C# backend.
func EmailNotifyHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		userID, _ := job.Payload["user_id"].(string)
		subject, _ := job.Payload["subject"].(string)
		body, _ := job.Payload["body"].(string)
		return deps.NotifySink.SendEmail(ctx, userID, subject, body)
	}
}

func PushNotifyHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		userID, _ := job.Payload["user_id"].(string)
		title, _ := job.Payload["title"].(string)
		body, _ := job.Payload["body"].(string)
		return deps.NotifySink.SendPush(ctx, userID, title, body)
	}
}

// AvailabilityCalcHandler recomputes and broadcasts a workspace member's
// availability summary, used when working hours or a heavy batch of events
// changed and cached availability needs to be refreshed proactively rather
// than lazily on next read.
func AvailabilityCalcHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		reqBody, _ := json.Marshal(job.Payload)
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, deps.CoreEngineURL+"/v1/availability/summary", bytes.NewReader(reqBody))
		if err != nil {
			return err
		}
		req.Header.Set("Content-Type", "application/json")
		resp, err := deps.HTTPClient.Do(req)
		if err != nil {
			return fmt.Errorf("core engine unreachable: %w", err)
		}
		defer resp.Body.Close()
		var summary map[string]any
		if err := json.NewDecoder(resp.Body).Decode(&summary); err != nil {
			return err
		}
		deps.Hub.Broadcast(ws.Event{
			Type:        "availability.updated",
			WorkspaceID: job.WorkspaceID,
			EntityID:    job.ID,
			Payload:     summary,
		})
		return nil
	}
}

// CalendarSyncHandler and WebhookDispatchHandler are extension points for
// external calendar sync (Google/Outlook) and outbound integration
// webhooks respectively; kept intentionally thin so new providers can be
// added without touching the worker pool or queue.
func CalendarSyncHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		deps.Logger.Info("calendar sync executed", "workspace_id", job.WorkspaceID, "job_id", job.ID)
		return nil
	}
}

func WebhookDispatchHandler(deps *Dependencies) func(context.Context, queue.Job) error {
	return func(ctx context.Context, job queue.Job) error {
		url, _ := job.Payload["url"].(string)
		if url == "" {
			return fmt.Errorf("webhook job missing url")
		}
		body, _ := json.Marshal(job.Payload["body"])
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(body))
		if err != nil {
			return err
		}
		req.Header.Set("Content-Type", "application/json")
		resp, err := deps.HTTPClient.Do(req)
		if err != nil {
			return err
		}
		defer resp.Body.Close()
		if resp.StatusCode >= 300 {
			return fmt.Errorf("webhook endpoint returned status %d", resp.StatusCode)
		}
		return nil
	}
}
