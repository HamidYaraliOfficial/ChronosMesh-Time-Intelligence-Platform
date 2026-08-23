# ChronosMesh — API Reference (v1)

Base URL: `https://<host>/api/v1` (Backend API, C#). Interactive Swagger
UI is available at `/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.

## Auth
| Method | Path | Description |
|---|---|---|
| POST | `/auth/register` | Create an account |
| POST | `/auth/login` | Exchange credentials for access + refresh tokens |
| POST | `/auth/refresh` | Rotate a refresh token for a new access token |
| POST | `/auth/logout` | Revoke a refresh token |

## Workspaces
| Method | Path | Description |
|---|---|---|
| POST | `/workspaces` | Create a workspace (caller becomes Owner) |
| GET | `/workspaces/{id}` | Get workspace details |

## Schedules & Availability
| Method | Path | Description |
|---|---|---|
| PUT | `/schedules/me` | Save the caller's weekly working hours |
| GET | `/schedules/me` | Get the caller's weekly working hours |
| GET | `/availability/me/summary` | Free intervals, next slot, remaining time today/week |

## Tasks / Events / Notifications
| Method | Path | Description |
|---|---|---|
| POST | `/tasks` | Create a task (optionally splittable, with deadline) |
| GET | `/tasks` | List tasks in the current workspace |
| POST | `/events` | Create a calendar event (optionally recurring) |
| GET | `/events?calendarId&startUtc&endUtc` | List events in a range |
| GET | `/notifications?unreadOnly` | List the caller's notifications |

## Scheduler Engine (Go, internal + WebSocket)
| Method | Path | Description |
|---|---|---|
| POST | `/v1/jobs` | Enqueue a background job (reminder, notification, sync) |
| GET | `/v1/ws?workspace_id&user_id` | Real-time WebSocket event stream |
| GET | `/v1/stats` | Queue depth / processed / failed / connected clients |

## Secure Core / Time Engine (Rust, internal)
| Method | Path | Description |
|---|---|---|
| POST | `/v1/availability/compute` | Free intervals for a working-hours + busy set + range |
| POST | `/v1/availability/summary` | Full availability summary (next slot, totals) |
| POST | `/v1/recurrence/expand` | Expand a recurring event into concrete occurrences |
| POST | `/v1/scheduling/allocate` | Smart-schedule tasks into free time |
| POST | `/v1/crypto/hash-password` / `/verify-password` | Argon2id password hashing |
| POST | `/v1/crypto/encrypt` / `/decrypt` | AES-256-GCM payload encryption |
