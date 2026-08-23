// ChronosMesh Scheduler Engine — the Go service responsible for background
// jobs, the notification/email/reminder queue, real-time WebSocket events,
// and heavy concurrent operations (recurring-event materialization,
// calendar sync, availability recalculation). See /docs/ARCHITECTURE.md.
package main

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"chronosmesh/scheduler/internal/api"
	"chronosmesh/scheduler/internal/config"
	"chronosmesh/scheduler/internal/jobs"
	"chronosmesh/scheduler/internal/queue"
	"chronosmesh/scheduler/internal/worker"
	"chronosmesh/scheduler/internal/ws"
)

func main() {
	cfg := config.Load()

	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{
		Level: parseLevel(cfg.LogLevel),
	}))
	slog.SetDefault(logger)

	q := queue.New(cfg.QueueCapacity)
	hub := ws.NewHub(logger)
	pool := worker.NewPool(q, cfg.WorkerCount,
		worker.WithJobTimeout(time.Duration(cfg.JobTimeoutSecs)*time.Second),
		worker.WithLogger(logger),
	)

	deps := &jobs.Dependencies{
		Hub:           hub,
		Logger:        logger,
		CoreEngineURL: cfg.CoreEngineURL,
		HTTPClient:    &http.Client{Timeout: 15 * time.Second},
		NotifySink:    &jobs.LoggingNotificationSink{Logger: logger},
	}

	pool.RegisterHandler(queue.JobReminder, jobs.ReminderHandler(deps))
	pool.RegisterHandler(queue.JobRecurringEvent, jobs.RecurringEventHandler(deps))
	pool.RegisterHandler(queue.JobEmailNotify, jobs.EmailNotifyHandler(deps))
	pool.RegisterHandler(queue.JobPushNotify, jobs.PushNotifyHandler(deps))
	pool.RegisterHandler(queue.JobCalendarSync, jobs.CalendarSyncHandler(deps))
	pool.RegisterHandler(queue.JobAvailabilityCalc, jobs.AvailabilityCalcHandler(deps))
	pool.RegisterHandler(queue.JobWebhookDispatch, jobs.WebhookDispatchHandler(deps))

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	go pool.Run(ctx)

	server := &api.Server{Queue: q, Pool: pool, Hub: hub, Logger: logger}
	httpServer := &http.Server{
		Addr:    ":" + cfg.Port,
		Handler: server.Routes(),
	}

	go func() {
		logger.Info("chronosmesh-scheduler listening", "port", cfg.Port, "workers", cfg.WorkerCount)
		if err := httpServer.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logger.Error("http server error", "error", err)
		}
	}()

	<-ctx.Done()
	logger.Info("shutting down gracefully")
	shutdownCtx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	_ = httpServer.Shutdown(shutdownCtx)
	q.Close()
}

func parseLevel(level string) slog.Level {
	switch level {
	case "debug":
		return slog.LevelDebug
	case "warn":
		return slog.LevelWarn
	case "error":
		return slog.LevelError
	default:
		return slog.LevelInfo
	}
}
