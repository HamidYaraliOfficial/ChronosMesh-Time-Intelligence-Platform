# ChronosMesh — Architecture

## Service map

| Service | Language | Responsibility |
|---|---|---|
| Desktop Client | C++ / Qt6 | Windows 11 native UI, calendar rendering, offline cache, drag & drop |
| Backend API | C# / ASP.NET Core | Business logic, auth, RBAC, workspace/task/event CRUD |
| Scheduler Engine | Go | Background jobs, reminders, notification queue, WebSocket real-time sync |
| Secure Core / Time Engine | Rust | Timezone/DST-safe availability & recurrence, crypto, conflict resolution |
| Web App | TypeScript / Next.js | Browser client mirroring desktop functionality |

## Request flow (example: "save my working hours")

1. Desktop (`AvailabilityWidget`) or Web (`/availability`) collects the
   weekly schedule and calls `PUT /api/v1/schedules/me` on the **C# API**.
2. C# persists the schedule via EF Core (`ScheduleRepository`) to Postgres.
3. To answer "how much free time do I have", C# calls the **Rust Secure
   Core** (`ITimeEngineClient` → `POST /v1/availability/summary`), which
   performs all timezone/DST-aware interval math and returns free
   intervals, next slot, and remaining time.
4. If a task has a deadline, C# enqueues a reminder job on the **Go
   Scheduler** (`ISchedulerQueueClient` → `POST /v1/jobs`). The Scheduler's
   worker pool processes it asynchronously and, on completion, broadcasts a
   `notification.reminder` event over its WebSocket hub so every connected
   client (desktop + web) updates in real time without polling.

## Why this split

- **Rust** owns every timezone/DST/leap-year/crypto calculation exactly
  once, so C#, Go, and the Desktop Client never duplicate — and never
  disagree on — this logic. It is exposed as a small internal HTTP service
  (`chronosmesh-core-server`) rather than compiled into each language,
  keeping the surface a single source of truth while still being callable
  from C++ directly via the `cdylib` target for the offline engine.
- **Go** owns everything that benefits from lightweight concurrency at
  scale: thousands of reminders firing in the same minute, WebSocket fan-out
  to every connected client in a workspace, retryable background jobs.
- **C#** owns the domain model, permissions, and orchestration — the parts
  that benefit most from a mature ORM (EF Core), strong typing, and
  ASP.NET Core's authentication/authorization pipeline.
- **C++/Qt6** delivers a first-class native Windows 11 experience
  (Fluent-style theming, offline mode, local SQLite cache) that a
  browser-only client cannot match.
- **TypeScript/Next.js** mirrors the same feature set for anyone without
  the desktop app installed, sharing the same backend contract.

## Data flow diagram (textual)

```
Desktop (Qt6) ─┐                                   ┌─ Web (Next.js)
               ├──HTTPS/JSON──> Backend API (C#) <──┤
               │                     │  │            │
               │                     │  └─HTTP──> Rust Core (Time Engine)
               │                     └─────HTTP──> Go Scheduler ──WS──> Desktop/Web
               └──────────────────WebSocket (real-time)───────────────┘
Backend API ──> PostgreSQL (source of truth)
Go Scheduler ──> Redis (queue/pubsub, optional horizontal scale-out)
```
