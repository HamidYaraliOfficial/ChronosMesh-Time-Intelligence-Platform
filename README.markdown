# ChronosMesh — Time Intelligence Platform

A polyglot, enterprise-grade platform for smart time management, scheduling, availability, and booking — built with **C++/Qt6**, **C#/ASP.NET Core**, **Go**, **Rust**, and **TypeScript/Next.js**, each owning a real, architected responsibility rather than a decorative one.

**Languages:** [English](#english) · [فارسی](#فارسی) · [中文](#中文)

---

## English

### 1. Project Overview

ChronosMesh is a **Time Intelligence Platform**: not a simple calendar, but a system that ingests everything about how you spend time — working hours, breaks, meetings, tasks, deadlines, holidays, timezones — and computes real answers: how much free time is actually left today, when the next available slot is, and how to fit a multi-hour task into the free time you actually have, split across days if needed.

The platform is deliberately polyglot. Each language was chosen for a real architectural reason, documented in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md):

- **Rust** — the Secure Core & Time Engine: timezone/DST/leap-year-safe availability and recurrence math, cryptography, conflict resolution.
- **Go** — the Scheduler Engine: background jobs, reminder/notification queues, WebSocket real-time sync, built for thousands of concurrent operations.
- **C#/ASP.NET Core** — the Backend API: business logic, authentication, RBAC, workspace/task/event management.
- **C++/Qt6** — the Desktop Client: a native, Windows 11–styled application with offline mode and a local cache.
- **TypeScript/Next.js** — the Web App: a browser client mirroring the desktop feature set.

### 2. Architecture

```
Desktop (Qt6) ─┐                                   ┌─ Web (Next.js)
               ├──HTTPS/JSON──> Backend API (C#) <──┤
               │                     │  │            │
               │                     │  └─HTTP──> Rust Core (Time Engine)
               │                     └─────HTTP──> Go Scheduler ──WS──> Desktop/Web
               └──────────────────WebSocket (real-time)───────────────┘
Backend API ──> PostgreSQL (source of truth)
Go Scheduler ──> Redis (queue/pub-sub, optional horizontal scale-out)
```

Full request-flow walkthroughs and design rationale live in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and
[`docs/TIME_ENGINE.md`](docs/TIME_ENGINE.md).

Repository layout:

```
/desktop     C++/Qt6 desktop client (CMake project)
/backend     C#/ASP.NET Core API (Clean Architecture: Domain/Application/Infrastructure/Api)
/scheduler   Go background-job & real-time service
/rust-core   Rust Secure Core / Time Engine (library + HTTP microservice)
/web         Next.js/TypeScript web application
/database    PostgreSQL schema.sql + seed.sql
/docker      docker-compose.yml + per-service Dockerfiles
/docs        Architecture, API, and Time Engine documentation
/tests       Cross-service test index (each service also has its own tests/ folder)
/scripts     setup.sh / run-dev.sh helper scripts
```

### 3. Technology Stack

| Layer | Technology |
|---|---|
| Desktop UI | C++20, Qt 6 (Widgets, Network, Sql), CMake |
| Backend API | C# / .NET 8, ASP.NET Core, EF Core, PostgreSQL, JWT, Serilog |
| Scheduler | Go 1.22, goroutines/channels, gorilla/websocket |
| Time Engine | Rust, chrono / chrono-tz, Axum, Argon2, AES-GCM |
| Web | Next.js 14, React 18, TypeScript, Recharts |
| Data | PostgreSQL 16, Redis 7 |
| Infra | Docker, Docker Compose |

### 4. Features

- Multi-view calendar (Day, Week, Month, Year, Timeline) with drag & drop rescheduling.
- Declarative working-hours input: which days you work, start/end times, breaks, unavailable windows — per user, per timezone.
- Availability Engine: free intervals, next available slot, total free time today/this week, remaining working time, minutes until next meeting.
- Smart Scheduling: tasks with duration, deadline, priority, and splittability are automatically allocated into real free time, split across multiple sessions when needed.
- Recurring events: daily, weekly, monthly, yearly, and custom recurrence, fully DST- and leap-year-safe.
- Booking system: define services (e.g. "30-minute consultation"), share a booking link, only real free slots are offered.
- Real-time sync: calendar/task changes propagate live to every connected client in a workspace via WebSocket.
- RBAC: Owner, Administrator, Manager, Member, Viewer roles with a granular resource/action permission matrix.
- Offline Mode (Desktop): local SQLite cache, queued pending changes, conflict resolution on reconnect.
- Analytics dashboard: working hours, meeting hours, focus time, free time, task completion, overtime, productivity.
- Global search across users, tasks, projects, events, bookings, teams, and workspaces.
- Notification Center: task reminders, meeting reminders, deadline warnings, booking confirmations, team updates, system alerts.
- Import/Export: CSV, JSON, ICS/iCalendar.
- Internationalization: English, Persian (فارسی, full RTL), and Chinese (中文), with a central translation system — no hard-coded UI strings.
- Theme Engine: Windows 11 Default, Light, Dark, Blue, and Red themes, extensible with new themes without recompilation.
- Command Palette (Ctrl+K), keyboard navigation, accessible labels, toasts, empty/loading/error states.

### 5. Installation

Prerequisites: Docker & Docker Compose (recommended path), or natively: Rust ≥ 1.75, Go ≥ 1.22, .NET 8 SDK, Node.js ≥ 20, Qt 6 + CMake ≥ 3.21 (desktop only), PostgreSQL 16, Redis 7.

**Option A — Docker Compose (recommended for backend services):**

```bash
cp docker/.env.example docker/.env
# edit docker/.env: set POSTGRES_PASSWORD, REDIS_PASSWORD,
# CHRONOSMESH_MASTER_KEY (32-byte hex, e.g. `openssl rand -hex 32`),
# and CHRONOSMESH_JWT_SECRET (e.g. `openssl rand -base64 48`)
bash scripts/run-dev.sh
```

This starts PostgreSQL, Redis, the Rust Core, the Go Scheduler, the C# Backend, and the Next.js Web app. The web app will be available at `http://localhost:3000`.

**Option B — Native, per-service (recommended while developing a single service):**

```bash
bash scripts/setup.sh   # builds/installs dependencies for every service present locally
```

Then run each service manually (see "Development" below).

**Desktop Client (Windows 11, native — not containerized):**

```bash
cd desktop
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --parallel
```

Requires Qt 6 (Widgets, Network, Sql, LinguistTools components) installed and discoverable by CMake (e.g. via `CMAKE_PREFIX_PATH`).

### 6. Development

```bash
# Rust Time Engine (hot-reload via cargo watch if installed)
cd rust-core && cargo run --bin chronosmesh-core-server

# Go Scheduler
cd scheduler && go run ./cmd/scheduler

# C# Backend API
cd backend && dotnet run --project src/ChronosMesh.Api

# Next.js Web App
cd web && npm run dev
```

The Backend API expects `CORE_ENGINE_URL` and `SCHEDULER_URL` to point at the Rust Core and Go Scheduler respectively (defaults assume Docker service names — override to `http://localhost:7301` / `http://localhost:8081` for fully-native local development).

### 7. Configuration

All configuration is environment-variable driven — no secrets in source control. See `docker/.env.example` for the full list with generation instructions for secrets.

### 8. Environment Variables

| Variable | Used by | Description |
|---|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Postgres, Backend | Database credentials |
| `REDIS_PASSWORD` | Redis, Scheduler | Cache/queue auth |
| `CHRONOSMESH_MASTER_KEY` | Rust Core | 32-byte hex AES-256-GCM key |
| `CHRONOSMESH_JWT_SECRET` | Backend | HMAC signing secret for access tokens |
| `CORE_ENGINE_URL` | Backend, Scheduler | Rust Core base URL |
| `SCHEDULER_URL` | Backend | Go Scheduler base URL |
| `SCHEDULER_WORKER_COUNT` | Scheduler | Worker pool concurrency |
| `ALLOWED_ORIGINS` | Backend | CORS allow-list |
| `NEXT_PUBLIC_API_URL` / `NEXT_PUBLIC_SCHEDULER_WS_URL` | Web | Public endpoints the browser calls |

### 9. Docker

`docker/docker-compose.yml` orchestrates Postgres, Redis, Rust Core, Go Scheduler, C# Backend, and the Next.js Web app, each with its own `docker/Dockerfile.*`. The Desktop Client is intentionally not containerized (it is a native Windows GUI application); build it via CMake as shown above.

```bash
cd docker
docker compose --env-file .env up --build
```

### 10. Database

Schema: [`database/schema.sql`](database/schema.sql) · Seed data (default RBAC matrix + demo workspace): [`database/seed.sql`](database/seed.sql).

In the running C# backend, the same model is expressed via EF Core (`backend/src/ChronosMesh.Infrastructure/Persistence/ChronosMeshDbContext.cs`); generate/apply migrations with:

```bash
cd backend
dotnet ef migrations add InitialCreate --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
dotnet ef database update --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
```

### 11. API

Versioned REST API under `/api/v1/*` (auth, users, workspaces, calendar, events, tasks, projects, bookings, schedules, availability, notifications, analytics). Full endpoint list: [`docs/API.md`](docs/API.md). Interactive Swagger UI at `/swagger` in development.

### 12. Testing

| Service | Command | What's covered |
|---|---|---|
| Rust Time Engine | `cd rust-core && cargo test` | DST transitions, leap years, recurrence, scheduling, crypto, conflict resolution — 24 tests |
| Go Scheduler | `cd scheduler && go test ./...` | Queue ordering/backpressure, worker pool concurrency & retry |
| C# Backend | `cd backend && dotnet test` | RBAC permission matrix, JWT/refresh-token issuance, password hashing |
| Web App | `cd web && npm test` | i18n dictionary consistency, RTL detection |
| Desktop Client | `ctest` in `desktop/build` (built with `-DBUILD_TESTING=ON`) | Theme Engine, Translation Manager (RTL/LTR) |

### 13. Build

Each service builds independently — see "Installation"/"Development" above. For production artifacts: `cargo build --release` (Rust), `go build` (Go, static binary), `dotnet publish -c Release` (C#), `npm run build` (Next.js), `cmake --build . --config Release` (Qt6 desktop, typically cross-compiled or built directly on Windows for distribution).

### 14. Deployment

The five backend-facing services (Postgres, Redis, Rust Core, Go Scheduler, C# Backend, Web) are designed to run as independent containers behind a reverse proxy / API gateway terminating TLS. The Desktop Client is distributed as a signed Windows installer built from the CMake project. Horizontal scaling: the Go Scheduler and C# Backend are stateless and can be scaled behind a load balancer; Redis backs cross-instance job/queue coordination.

### 15. Security

- Passwords are never stored or logged in plain text — hashed with BCrypt (backend) / Argon2id (Rust core, for the offline engine).
- JWT access tokens are short-lived (15 minutes); refresh tokens are single-use, stored server-side only as a SHA-256 hash, and rotated on every use.
- RBAC enforced both at the API layer (ASP.NET Core policies) and via a shared, testable permission matrix.
- Rate limiting on auth and write endpoints (AspNetCoreRateLimit).
- Full audit log for permission changes, role changes, and deletions.
- AES-256-GCM for secrets at rest via the Rust Secure Core.
- All configuration/secrets are environment-variable driven — nothing sensitive is committed to source control.

### 16. Internationalization

Three languages ship out of the box: English (LTR), Persian/فارسی (full RTL), and Chinese/中文 (LTR). No UI string is hard-coded — the Desktop Client routes every string through Qt's translation system (`.ts`/`.qm` files in `desktop/resources/translations/`), and the Web App through a central dictionary (`web/lib/i18n/dictionaries.ts`). Layout direction cascades automatically from the selected language to every menu, dialog, toolbar, sidebar, form, calendar grid, and notification — never hard-coded per component.

### 17. Theme System

Five themes ship out of the box: **Windows 11 Default**, **Light**, **Dark**, **Blue**, and **Red**. The Desktop Client's Theme Engine (`desktop/src/ThemeManager.*`) applies plain Qt Style Sheets and can register additional themes at runtime without recompilation. The Web App mirrors this with a CSS-custom-property `ThemeProvider` (`web/components/Theme/ThemeProvider.tsx`) — components never hard-code colors, only `var(--cm-*)`.

### 18. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Backend fails to start with a JWT secret error | Set `CHRONOSMESH_JWT_SECRET` in `docker/.env` or your shell environment |
| Rust Core returns 400 on availability calls | Check the `timezone` field is a valid IANA name (e.g. `Europe/Berlin`, not `CET`) |
| WebSocket never connects from the Web App | Confirm `NEXT_PUBLIC_SCHEDULER_WS_URL` matches the Scheduler's exposed port and scheme (`ws://` vs `wss://`) |
| `go test` fails to fetch modules | Ensure `GOPROXY=direct GOSUMDB=off` (or a reachable module proxy) is set in restricted network environments |
| Desktop build fails to find Qt6 | Set `CMAKE_PREFIX_PATH` to your Qt6 installation, e.g. `-DCMAKE_PREFIX_PATH=C:/Qt/6.7.0/msvc2019_64` |
| Postgres seed data missing | `database/seed.sql` only runs on first container init — drop the `chronosmesh_postgres_data` volume to re-seed |

---

## فارسی

### ۱. معرفی پروژه

ChronosMesh یک **پلتفرم هوش زمانی** است: نه یک تقویم ساده، بلکه سیستمی که تمام اطلاعات مربوط به نحوه گذران وقت شما — ساعات کاری، استراحت‌ها، جلسات، وظایف، Deadlineها، تعطیلات، منطقه‌های زمانی — را دریافت می‌کند و پاسخ‌های واقعی محاسبه می‌کند: چقدر زمان آزاد واقعاً امروز باقی مانده، بازه زمانی آزاد بعدی کِی است، و چگونه یک وظیفه چند ساعته را در زمان آزاد واقعی شما جای دهد، در صورت نیاز آن را بین چند روز تقسیم کند.

این پلتفرم عمداً چندزبانه طراحی شده است. هر زبان به دلیلی معماری واقعی انتخاب شده که در [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) مستند شده است:

- **Rust** — هسته امن و موتور زمان: محاسبات در دسترس بودن و تکرار رویداد با پشتیبانی کامل از منطقه زمانی/DST/سال کبیسه، رمزنگاری، حل تعارض.
- **Go** — موتور زمان‌بندی: پردازش‌های پس‌زمینه، صف یادآوری/اعلان، همگام‌سازی Real-Time با WebSocket، طراحی‌شده برای هزاران عملیات همزمان.
- **C#/ASP.NET Core** — Backend اصلی: منطق کسب‌وکار، احراز هویت، RBAC، مدیریت Workspace/Task/Event.
- **C++/Qt6** — کلاینت دسکتاپ: یک اپلیکیشن بومی با ظاهر ویندوز ۱۱، دارای حالت آفلاین و Cache محلی.
- **TypeScript/Next.js** — اپلیکیشن وب: کلاینت مرورگر با همان قابلیت‌های دسکتاپ.

### ۲. معماری

```
دسکتاپ (Qt6) ─┐                                   ┌─ وب (Next.js)
              ├──HTTPS/JSON──> Backend API (C#) <──┤
              │                     │  │            │
              │                     │  └─HTTP──> هسته Rust (موتور زمان)
              │                     └─────HTTP──> زمان‌بند Go ──WS──> دسکتاپ/وب
              └──────────────────WebSocket (Real-Time)───────────────┘
Backend API ──> PostgreSQL (منبع اصلی داده)
زمان‌بند Go ──> Redis (صف/pub-sub، مقیاس‌پذیری افقی اختیاری)
```

توضیحات کامل جریان درخواست‌ها و دلایل طراحی در [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) و [`docs/TIME_ENGINE.md`](docs/TIME_ENGINE.md) موجود است.

ساختار Repository:

```
/desktop     کلاینت دسکتاپ C++/Qt6 (پروژه CMake)
/backend     API با C#/ASP.NET Core (معماری تمیز: Domain/Application/Infrastructure/Api)
/scheduler   سرویس Go برای Job های پس‌زمینه و Real-Time
/rust-core   هسته امن Rust / موتور زمان (کتابخانه + میکروسرویس HTTP)
/web         اپلیکیشن وب Next.js/TypeScript
/database    schema.sql و seed.sql برای PostgreSQL
/docker      docker-compose.yml و Dockerfile هر سرویس
/docs        مستندات معماری، API و موتور زمان
/tests       فهرست تست‌های بین‌سرویسی (هر سرویس پوشه tests/ مخصوص خود را نیز دارد)
/scripts     اسکریپت‌های کمکی setup.sh / run-dev.sh
```

### ۳. پشته فناوری

| لایه | فناوری |
|---|---|
| رابط دسکتاپ | C++20، Qt 6 (Widgets، Network، Sql)، CMake |
| Backend API | C# / .NET 8، ASP.NET Core، EF Core، PostgreSQL، JWT، Serilog |
| زمان‌بند | Go 1.22، goroutine/channel، gorilla/websocket |
| موتور زمان | Rust، chrono / chrono-tz، Axum، Argon2، AES-GCM |
| وب | Next.js 14، React 18، TypeScript، Recharts |
| داده | PostgreSQL 16، Redis 7 |
| زیرساخت | Docker، Docker Compose |

### ۴. ویژگی‌ها

- تقویم چند‌نمایی (روز، هفته، ماه، سال، خط زمانی) با قابلیت Drag & Drop.
- ورودی اعلامی ساعات کاری: چه روزهایی کار می‌کنید، ساعت شروع/پایان، استراحت‌ها، بازه‌های غیرقابل‌دسترس — به تفکیک کاربر و منطقه زمانی.
- موتور در دسترس بودن: بازه‌های آزاد، زمان آزاد بعدی، مجموع زمان آزاد امروز/این هفته، زمان کاری باقی‌مانده، زمان تا جلسه بعدی.
- برنامه‌ریزی هوشمند: وظایف با مدت زمان، Deadline، اولویت و قابلیت تقسیم‌شدن به‌طور خودکار در زمان آزاد واقعی جای‌گذاری می‌شوند، در صورت نیاز بین چند جلسه تقسیم می‌شوند.
- رویدادهای تکرارشونده: روزانه، هفتگی، ماهانه، سالانه و سفارشی، با پشتیبانی کامل از DST و سال کبیسه.
- سیستم رزرو: تعریف سرویس‌ها (مثلاً «مشاوره ۳۰ دقیقه‌ای»)، اشتراک‌گذاری لینک رزرو، فقط بازه‌های واقعاً آزاد نمایش داده می‌شوند.
- همگام‌سازی Real-Time: تغییرات تقویم/وظایف به‌صورت زنده به تمام کلاینت‌های متصل در یک Workspace از طریق WebSocket منتقل می‌شود.
- RBAC: نقش‌های Owner، Administrator، Manager، Member، Viewer با ماتریس دقیق مجوز منبع/عملیات.
- حالت آفلاین (دسکتاپ): Cache محلی SQLite، صف تغییرات در انتظار، حل تعارض هنگام اتصال مجدد.
- داشبورد تحلیلی: ساعات کاری، ساعات جلسات، زمان تمرکز، زمان آزاد، تکمیل وظایف، اضافه‌کاری، بهره‌وری.
- جستجوی سراسری در بین کاربران، وظایف، پروژه‌ها، رویدادها، رزروها، تیم‌ها و Workspaceها.
- مرکز اعلان: یادآوری وظیفه، یادآوری جلسه، هشدار Deadline، تأیید رزرو، به‌روزرسانی تیم، هشدار سیستم.
- Import/Export: CSV، JSON، ICS/iCalendar.
- چندزبانگی: انگلیسی، فارسی (RTL کامل) و چینی، با سیستم ترجمه مرکزی — بدون هیچ متن Hard-Code شده در رابط کاربری.
- Theme Engine: تم‌های Windows 11 Default، Light، Dark، Blue و Red، قابل توسعه با تم‌های جدید بدون نیاز به Compile مجدد.
- Command Palette (با Ctrl+K)، ناوبری با صفحه‌کلید، برچسب‌های در دسترس، Toast، حالت‌های خالی/بارگذاری/خطا.

### ۵. نصب

پیش‌نیازها: Docker و Docker Compose (روش پیشنهادی)، یا به‌صورت بومی: Rust نسخه ۱.۷۵ به بالا، Go نسخه ۱.۲۲ به بالا، .NET 8 SDK، Node.js نسخه ۲۰ به بالا، Qt 6 و CMake نسخه ۳.۲۱ به بالا (فقط برای دسکتاپ)، PostgreSQL 16، Redis 7.

**روش A — با Docker Compose (پیشنهادی برای سرویس‌های Backend):**

```bash
cp docker/.env.example docker/.env
# فایل docker/.env را ویرایش کنید: مقدار POSTGRES_PASSWORD، REDIS_PASSWORD،
# CHRONOSMESH_MASTER_KEY (رشته هگز ۳۲ بایتی، مثلاً با `openssl rand -hex 32`)،
# و CHRONOSMESH_JWT_SECRET (مثلاً با `openssl rand -base64 48`) را تنظیم کنید
bash scripts/run-dev.sh
```

این دستور PostgreSQL، Redis، هسته Rust، زمان‌بند Go، Backend با C#، و اپلیکیشن وب Next.js را اجرا می‌کند. اپلیکیشن وب در آدرس `http://localhost:3000` در دسترس خواهد بود.

**روش B — نصب بومی هر سرویس (پیشنهادی هنگام توسعه یک سرویس خاص):**

```bash
bash scripts/setup.sh   # وابستگی‌های هر سرویس موجود را نصب/Build می‌کند
```

سپس هر سرویس را به‌صورت دستی اجرا کنید (بخش «توسعه» را ببینید).

**کلاینت دسکتاپ (ویندوز ۱۱، بومی — بدون Container):**

```bash
cd desktop
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --parallel
```

نیازمند نصب Qt 6 (کامپوننت‌های Widgets، Network، Sql، LinguistTools) و قابل تشخیص بودن آن توسط CMake است (مثلاً از طریق `CMAKE_PREFIX_PATH`).

### ۶. توسعه

```bash
# موتور زمان Rust
cd rust-core && cargo run --bin chronosmesh-core-server

# زمان‌بند Go
cd scheduler && go run ./cmd/scheduler

# Backend API با C#
cd backend && dotnet run --project src/ChronosMesh.Api

# اپلیکیشن وب Next.js
cd web && npm run dev
```

Backend API انتظار دارد `CORE_ENGINE_URL` و `SCHEDULER_URL` به‌ترتیب به هسته Rust و زمان‌بند Go اشاره کنند (مقادیر پیش‌فرض بر اساس نام سرویس‌های Docker هستند — برای توسعه کاملاً بومی به `http://localhost:7301` و `http://localhost:8081` تغییر دهید).

### ۷. پیکربندی

تمام پیکربندی‌ها از طریق متغیرهای محیطی (Environment Variables) انجام می‌شود — هیچ Secret‌ای در Source Control ذخیره نمی‌شود. فایل `docker/.env.example` را برای فهرست کامل به همراه راهنمای تولید Secretها ببینید.

### ۸. متغیرهای محیطی

| متغیر | استفاده در | توضیح |
|---|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Postgres، Backend | اطلاعات ورود به دیتابیس |
| `REDIS_PASSWORD` | Redis، Scheduler | احراز هویت Cache/صف |
| `CHRONOSMESH_MASTER_KEY` | هسته Rust | کلید هگز ۳۲ بایتی برای AES-256-GCM |
| `CHRONOSMESH_JWT_SECRET` | Backend | Secret امضای HMAC برای Access Token |
| `CORE_ENGINE_URL` | Backend، Scheduler | آدرس پایه هسته Rust |
| `SCHEDULER_URL` | Backend | آدرس پایه زمان‌بند Go |
| `SCHEDULER_WORKER_COUNT` | Scheduler | تعداد Worker همزمان |
| `ALLOWED_ORIGINS` | Backend | فهرست مجاز CORS |
| `NEXT_PUBLIC_API_URL` / `NEXT_PUBLIC_SCHEDULER_WS_URL` | وب | Endpointهای عمومی که مرورگر فراخوانی می‌کند |

### ۹. Docker

فایل `docker/docker-compose.yml` سرویس‌های Postgres، Redis، هسته Rust، زمان‌بند Go، Backend با C#، و اپلیکیشن وب Next.js را هماهنگ می‌کند، هرکدام با `docker/Dockerfile.*` مخصوص خود. کلاینت دسکتاپ عمداً Containerized نشده (یک اپلیکیشن بومی گرافیکی ویندوز است)؛ آن را طبق دستورات بالا با CMake بسازید.

```bash
cd docker
docker compose --env-file .env up --build
```

### ۱۰. دیتابیس

Schema: [`database/schema.sql`](database/schema.sql) · داده اولیه (ماتریس پیش‌فرض RBAC + Workspace نمونه): [`database/seed.sql`](database/seed.sql).

در Backend در حال اجرا با C#، همان مدل از طریق EF Core بیان می‌شود (`backend/src/ChronosMesh.Infrastructure/Persistence/ChronosMeshDbContext.cs`)؛ Migrationها را با دستورات زیر تولید/اعمال کنید:

```bash
cd backend
dotnet ef migrations add InitialCreate --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
dotnet ef database update --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
```

### ۱۱. API

API نسخه‌بندی‌شده REST تحت مسیر `/api/v1/*` (auth، users، workspaces، calendar، events، tasks، projects، bookings، schedules، availability، notifications، analytics). فهرست کامل Endpointها: [`docs/API.md`](docs/API.md). رابط تعاملی Swagger در مسیر `/swagger` در محیط توسعه در دسترس است.

### ۱۲. تست

| سرویس | دستور | پوشش تست |
|---|---|---|
| موتور زمان Rust | `cd rust-core && cargo test` | انتقال DST، سال کبیسه، تکرار رویداد، برنامه‌ریزی، رمزنگاری، حل تعارض — ۲۴ تست |
| زمان‌بند Go | `cd scheduler && go test ./...` | ترتیب صف/Backpressure، همزمانی Worker Pool و تلاش مجدد |
| Backend با C# | `cd backend && dotnet test` | ماتریس مجوز RBAC، صدور JWT/Refresh Token، Hash کردن رمز عبور |
| اپلیکیشن وب | `cd web && npm test` | یکپارچگی دیکشنری چندزبانه، تشخیص RTL |
| کلاینت دسکتاپ | `ctest` در `desktop/build` (Build شده با `-DBUILD_TESTING=ON`) | Theme Engine، مدیریت ترجمه (RTL/LTR) |

### ۱۳. Build

هر سرویس مستقل Build می‌شود — بخش‌های «نصب»/«توسعه» را ببینید. برای خروجی Production: `cargo build --release` (Rust)، `go build` (Go، باینری Static)، `dotnet publish -c Release` (C#)، `npm run build` (Next.js)، `cmake --build . --config Release` (دسکتاپ Qt6، معمولاً به‌صورت مستقیم روی ویندوز برای توزیع Build می‌شود).

### ۱۴. استقرار (Deployment)

پنج سرویس مرتبط با Backend (Postgres، Redis، هسته Rust، زمان‌بند Go، Backend با C#، وب) به‌صورت Containerهای مستقل پشت یک Reverse Proxy/API Gateway که TLS را Terminate می‌کند طراحی شده‌اند. کلاینت دسکتاپ به‌صورت یک Installer امضاشده برای ویندوز، ساخته‌شده از پروژه CMake، توزیع می‌شود. مقیاس‌پذیری افقی: زمان‌بند Go و Backend با C# Stateless هستند و می‌توانند پشت یک Load Balancer مقیاس‌پذیر شوند؛ Redis هماهنگی صف/Job بین نمونه‌ها را پشتیبانی می‌کند.

### ۱۵. امنیت

- رمزهای عبور هرگز به‌صورت متن ساده ذخیره یا Log نمی‌شوند — با BCrypt (Backend) / Argon2id (هسته Rust، برای موتور آفلاین) Hash می‌شوند.
- Access Tokenهای JWT کوتاه‌مدت هستند (۱۵ دقیقه)؛ Refresh Tokenها تک‌مصرفی هستند، فقط به‌صورت Hash با SHA-256 در سمت سرور ذخیره می‌شوند، و در هر استفاده Rotate می‌شوند.
- RBAC هم در لایه API (Policyهای ASP.NET Core) و هم از طریق یک ماتریس مجوز مشترک و قابل تست اعمال می‌شود.
- Rate Limiting روی Endpointهای احراز هویت و نوشتن (با AspNetCoreRateLimit).
- Audit Log کامل برای تغییرات مجوز، تغییرات نقش و حذف‌ها.
- AES-256-GCM برای Secretهای ذخیره‌شده از طریق هسته امن Rust.
- تمام پیکربندی/Secretها از طریق متغیرهای محیطی تنظیم می‌شوند — هیچ اطلاعات حساسی در Source Control ذخیره نمی‌شود.

### ۱۶. بین‌المللی‌سازی (Internationalization)

سه زبان به‌صورت پیش‌فرض پشتیبانی می‌شوند: انگلیسی (LTR)، فارسی (RTL کامل) و چینی (LTR). هیچ متن رابط کاربری Hard-Code نشده است — کلاینت دسکتاپ تمام متن‌ها را از طریق سیستم ترجمه Qt (فایل‌های `.ts`/`.qm` در `desktop/resources/translations/`) عبور می‌دهد، و اپلیکیشن وب از طریق یک دیکشنری مرکزی (`web/lib/i18n/dictionaries.ts`). جهت Layout به‌طور خودکار از زبان انتخاب‌شده به تمام Menu، Dialog، Toolbar، Sidebar، Form، شبکه تقویم و اعلان‌ها اعمال می‌شود — هرگز به‌صورت جداگانه در هر Component Hard-Code نشده است.

### ۱۷. سیستم Theme

پنج تم به‌صورت پیش‌فرض پشتیبانی می‌شوند: **Windows 11 Default**، **Light**، **Dark**، **Blue** و **Red**. Theme Engine کلاینت دسکتاپ (`desktop/src/ThemeManager.*`) از Qt Style Sheetهای ساده استفاده می‌کند و می‌تواند تم‌های جدید را در زمان اجرا بدون نیاز به Compile مجدد ثبت کند. اپلیکیشن وب همین رویکرد را با یک `ThemeProvider` مبتنی بر CSS Custom Property بازتاب می‌دهد (`web/components/Theme/ThemeProvider.tsx`) — Componentها هرگز رنگ را به‌صورت مستقیم Hard-Code نمی‌کنند، فقط از `var(--cm-*)` استفاده می‌کنند.

### ۱۸. رفع اشکال (Troubleshooting)

| علامت | علت احتمالی / راه‌حل |
|---|---|
| Backend با خطای JWT Secret اجرا نمی‌شود | مقدار `CHRONOSMESH_JWT_SECRET` را در `docker/.env` یا محیط Shell خود تنظیم کنید |
| هسته Rust برای درخواست‌های Availability خطای ۴۰۰ برمی‌گرداند | بررسی کنید فیلد `timezone` یک نام معتبر IANA باشد (مثلاً `Europe/Berlin`، نه `CET`) |
| WebSocket از اپلیکیشن وب هرگز متصل نمی‌شود | بررسی کنید `NEXT_PUBLIC_SCHEDULER_WS_URL` با پورت و Scheme (`ws://` در برابر `wss://`) در دسترس زمان‌بند مطابقت داشته باشد |
| دستور `go test` نمی‌تواند Moduleها را دریافت کند | در محیط‌های شبکه محدود، `GOPROXY=direct GOSUMDB=off` (یا یک Proxy Module در دسترس) را تنظیم کنید |
| Build دسکتاپ Qt6 را پیدا نمی‌کند | `CMAKE_PREFIX_PATH` را به مسیر نصب Qt6 تنظیم کنید، مثلاً `-DCMAKE_PREFIX_PATH=C:/Qt/6.7.0/msvc2019_64` |
| داده اولیه Postgres موجود نیست | `database/seed.sql` فقط در اولین راه‌اندازی Container اجرا می‌شود — برای اجرای مجدد، Volume با نام `chronosmesh_postgres_data` را حذف کنید |

---

## 中文

### 1. 项目概述

ChronosMesh 是一个**时间智能平台**：它不是一个简单的日历，而是一个能够汇总您所有时间使用信息——工作时间、休息时间、会议、任务、截止日期、假期、时区——并计算出真实答案的系统：今天究竟还剩多少空闲时间、下一个可用时段是什么时候，以及如何将一项耗时数小时的任务安排进您实际拥有的空闲时间中，必要时可跨天拆分。

该平台采用有意为之的多语言架构。每种编程语言的选择都有真实的架构原因，详见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)：

- **Rust** — 安全核心与时间引擎：时区/夏令时/闰年安全的可用性与重复事件计算、加密、冲突解决。
- **Go** — 调度引擎：后台任务、提醒/通知队列、基于 WebSocket 的实时同步，专为数千并发操作而设计。
- **C#/ASP.NET Core** — 后端 API：业务逻辑、身份验证、RBAC、工作区/任务/事件管理。
- **C++/Qt6** — 桌面客户端：原生的 Windows 11 风格应用程序，支持离线模式与本地缓存。
- **TypeScript/Next.js** — Web 应用：镜像桌面端功能的浏览器客户端。

### 2. 架构

```
桌面端 (Qt6) ─┐                                   ┌─ Web 端 (Next.js)
              ├──HTTPS/JSON──> 后端 API (C#) <──┤
              │                     │  │            │
              │                     │  └─HTTP──> Rust 核心 (时间引擎)
              │                     └─────HTTP──> Go 调度器 ──WS──> 桌面端/Web 端
              └──────────────────WebSocket (实时)───────────────┘
后端 API ──> PostgreSQL（数据源）
Go 调度器 ──> Redis（队列/发布订阅，可选水平扩展）
```

完整的请求流程说明与设计理由参见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) 与 [`docs/TIME_ENGINE.md`](docs/TIME_ENGINE.md)。

仓库结构：

```
/desktop     C++/Qt6 桌面客户端（CMake 项目）
/backend     C#/ASP.NET Core API（整洁架构：Domain/Application/Infrastructure/Api）
/scheduler   Go 后台任务与实时服务
/rust-core   Rust 安全核心 / 时间引擎（库 + HTTP 微服务）
/web         Next.js/TypeScript Web 应用
/database    PostgreSQL 的 schema.sql 与 seed.sql
/docker      docker-compose.yml 及各服务 Dockerfile
/docs        架构、API 与时间引擎文档
/tests       跨服务测试索引（每个服务也有各自的 tests/ 目录）
/scripts     setup.sh / run-dev.sh 辅助脚本
```

### 3. 技术栈

| 层级 | 技术 |
|---|---|
| 桌面界面 | C++20、Qt 6（Widgets、Network、Sql）、CMake |
| 后端 API | C# / .NET 8、ASP.NET Core、EF Core、PostgreSQL、JWT、Serilog |
| 调度器 | Go 1.22、goroutine/channel、gorilla/websocket |
| 时间引擎 | Rust、chrono / chrono-tz、Axum、Argon2、AES-GCM |
| Web | Next.js 14、React 18、TypeScript、Recharts |
| 数据 | PostgreSQL 16、Redis 7 |
| 基础设施 | Docker、Docker Compose |

### 4. 功能特性

- 多视图日历（日、周、月、年、时间线），支持拖放重新安排。
- 声明式工作时间输入：您工作的日子、开始/结束时间、休息时间、不可用时段——按用户、按时区分别设置。
- 可用性引擎：空闲时段、下一个可用时段、今日/本周总空闲时间、剩余工作时间、距下次会议的时间。
- 智能调度：具有时长、截止日期、优先级和可拆分性的任务会自动分配到真实的空闲时间中，必要时拆分到多个时段。
- 重复事件：每日、每周、每月、每年及自定义重复规则，完全支持夏令时与闰年安全。
- 预约系统：定义服务（例如"30分钟咨询"），分享预约链接，仅展示真正空闲的时段。
- 实时同步：日历/任务的变更通过 WebSocket 实时同步到工作区内所有已连接的客户端。
- RBAC：所有者（Owner）、管理员（Administrator）、经理（Manager）、成员（Member）、访客（Viewer）角色，配有细粒度的资源/操作权限矩阵。
- 离线模式（桌面端）：本地 SQLite 缓存、待处理更改队列、重新连接后的冲突解决。
- 分析仪表盘：工作时间、会议时间、专注时间、空闲时间、任务完成率、加班时长、生产力。
- 全局搜索，覆盖用户、任务、项目、事件、预约、团队和工作区。
- 通知中心：任务提醒、会议提醒、截止日期警告、预约确认、团队更新、系统警报。
- 导入/导出：CSV、JSON、ICS/iCalendar。
- 国际化：英语、波斯语（فارسی，完整从右到左 RTL）和中文，采用中央翻译系统——界面中没有硬编码文本。
- 主题引擎：Windows 11 默认、浅色、深色、蓝色、红色五种主题，可在不重新编译的情况下扩展新主题。
- 命令面板（Ctrl+K）、键盘导航、无障碍标签、消息提示（Toast）、空状态/加载状态/错误状态。

### 5. 安装

前置条件：Docker 与 Docker Compose（推荐方式），或原生安装：Rust ≥ 1.75、Go ≥ 1.22、.NET 8 SDK、Node.js ≥ 20、Qt 6 与 CMake ≥ 3.21（仅桌面端需要）、PostgreSQL 16、Redis 7。

**方式 A — 使用 Docker Compose（推荐用于后端服务）：**

```bash
cp docker/.env.example docker/.env
# 编辑 docker/.env：设置 POSTGRES_PASSWORD、REDIS_PASSWORD、
# CHRONOSMESH_MASTER_KEY（32字节十六进制，例如用 `openssl rand -hex 32` 生成）、
# 以及 CHRONOSMESH_JWT_SECRET（例如用 `openssl rand -base64 48` 生成）
bash scripts/run-dev.sh
```

此命令会启动 PostgreSQL、Redis、Rust 核心、Go 调度器、C# 后端以及 Next.js Web 应用。Web 应用将在 `http://localhost:3000` 上可用。

**方式 B — 各服务原生安装（推荐用于单个服务的开发）：**

```bash
bash scripts/setup.sh   # 为本地存在的每个服务构建/安装依赖
```

然后手动运行每个服务（见下方"开发"部分）。

**桌面客户端（Windows 11，原生 — 不容器化）：**

```bash
cd desktop
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --parallel
```

需要安装 Qt 6（Widgets、Network、Sql、LinguistTools 组件）并可被 CMake 发现（例如通过 `CMAKE_PREFIX_PATH`）。

### 6. 开发

```bash
# Rust 时间引擎
cd rust-core && cargo run --bin chronosmesh-core-server

# Go 调度器
cd scheduler && go run ./cmd/scheduler

# C# 后端 API
cd backend && dotnet run --project src/ChronosMesh.Api

# Next.js Web 应用
cd web && npm run dev
```

后端 API 期望 `CORE_ENGINE_URL` 和 `SCHEDULER_URL` 分别指向 Rust 核心和 Go 调度器（默认值假定使用 Docker 服务名——在完全原生的本地开发中请改为 `http://localhost:7301` 与 `http://localhost:8081`）。

### 7. 配置

所有配置均通过环境变量驱动——源代码中不包含任何密钥。完整列表及密钥生成说明请参见 `docker/.env.example`。

### 8. 环境变量

| 变量 | 使用方 | 说明 |
|---|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Postgres、后端 | 数据库凭据 |
| `REDIS_PASSWORD` | Redis、调度器 | 缓存/队列身份验证 |
| `CHRONOSMESH_MASTER_KEY` | Rust 核心 | 32字节十六进制 AES-256-GCM 密钥 |
| `CHRONOSMESH_JWT_SECRET` | 后端 | 访问令牌的 HMAC 签名密钥 |
| `CORE_ENGINE_URL` | 后端、调度器 | Rust 核心的基础 URL |
| `SCHEDULER_URL` | 后端 | Go 调度器的基础 URL |
| `SCHEDULER_WORKER_COUNT` | 调度器 | 工作池并发数 |
| `ALLOWED_ORIGINS` | 后端 | CORS 允许列表 |
| `NEXT_PUBLIC_API_URL` / `NEXT_PUBLIC_SCHEDULER_WS_URL` | Web | 浏览器调用的公共端点 |

### 9. Docker

`docker/docker-compose.yml` 编排 Postgres、Redis、Rust 核心、Go 调度器、C# 后端和 Next.js Web 应用，每个服务都有自己的 `docker/Dockerfile.*`。桌面客户端有意不进行容器化（它是原生的 Windows 图形界面应用程序）；请按上文所述使用 CMake 构建。

```bash
cd docker
docker compose --env-file .env up --build
```

### 10. 数据库

架构文件：[`database/schema.sql`](database/schema.sql) · 种子数据（默认 RBAC 矩阵 + 演示工作区）：[`database/seed.sql`](database/seed.sql)。

在运行中的 C# 后端中，相同的模型通过 EF Core 表达（`backend/src/ChronosMesh.Infrastructure/Persistence/ChronosMeshDbContext.cs`）；使用以下命令生成/应用迁移：

```bash
cd backend
dotnet ef migrations add InitialCreate --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
dotnet ef database update --project src/ChronosMesh.Infrastructure --startup-project src/ChronosMesh.Api
```

### 11. API

版本化的 REST API，路径前缀为 `/api/v1/*`（auth、users、workspaces、calendar、events、tasks、projects、bookings、schedules、availability、notifications、analytics）。完整端点列表见 [`docs/API.md`](docs/API.md)。开发环境下可在 `/swagger` 访问交互式 Swagger 界面。

### 12. 测试

| 服务 | 命令 | 覆盖内容 |
|---|---|---|
| Rust 时间引擎 | `cd rust-core && cargo test` | 夏令时转换、闰年、重复事件、调度、加密、冲突解决——共24个测试 |
| Go 调度器 | `cd scheduler && go test ./...` | 队列顺序/背压、工作池并发与重试 |
| C# 后端 | `cd backend && dotnet test` | RBAC 权限矩阵、JWT/刷新令牌签发、密码哈希 |
| Web 应用 | `cd web && npm test` | 国际化词典一致性、RTL 检测 |
| 桌面客户端 | 在 `desktop/build` 中运行 `ctest`（需以 `-DBUILD_TESTING=ON` 构建） | 主题引擎、翻译管理器（RTL/LTR） |

### 13. 构建

每个服务独立构建——见上文"安装"/"开发"部分。生产环境构建产物：`cargo build --release`（Rust）、`go build`（Go，静态二进制文件）、`dotnet publish -c Release`（C#）、`npm run build`（Next.js）、`cmake --build . --config Release`（Qt6 桌面端，通常直接在 Windows 上构建以用于分发）。

### 14. 部署

五个面向后端的服务（Postgres、Redis、Rust 核心、Go 调度器、C# 后端、Web）设计为在反向代理/API 网关（负责终止 TLS）后作为独立容器运行。桌面客户端以从 CMake 项目构建的已签名 Windows 安装程序形式分发。水平扩展：Go 调度器和 C# 后端是无状态的，可以在负载均衡器后进行扩展；Redis 支持跨实例的任务/队列协调。

### 15. 安全

- 密码永远不会以明文形式存储或记录日志——使用 BCrypt（后端）/ Argon2id（Rust 核心，用于离线引擎）进行哈希处理。
- JWT 访问令牌生命周期较短（15分钟）；刷新令牌为一次性使用，仅在服务器端以 SHA-256 哈希形式存储，并在每次使用后轮换。
- RBAC 在 API 层（ASP.NET Core 策略）和共享的、可测试的权限矩阵中均得到强制执行。
- 对身份验证和写入端点实施速率限制（使用 AspNetCoreRateLimit）。
- 针对权限变更、角色变更和删除操作的完整审计日志。
- 通过 Rust 安全核心对静态密钥使用 AES-256-GCM 加密。
- 所有配置/密钥均通过环境变量驱动——源代码控制中不包含任何敏感信息。

### 16. 国际化

平台默认支持三种语言：英语（从左到右 LTR）、波斯语/فارسی（完整从右到左 RTL）和中文（从左到右 LTR）。界面中没有硬编码文本——桌面客户端将所有文本通过 Qt 的翻译系统（`desktop/resources/translations/` 中的 `.ts`/`.qm` 文件）处理，Web 应用则通过中央词典（`web/lib/i18n/dictionaries.ts`）处理。布局方向会根据所选语言自动级联应用到所有菜单、对话框、工具栏、侧边栏、表单、日历网格和通知——绝不在各个组件中单独硬编码。

### 17. 主题系统

平台默认提供五种主题：**Windows 11 默认**、**浅色**、**深色**、**蓝色**和**红色**。桌面客户端的主题引擎（`desktop/src/ThemeManager.*`）应用纯 Qt 样式表，并可以在运行时注册额外的主题，无需重新编译。Web 应用通过基于 CSS 自定义属性的 `ThemeProvider`（`web/components/Theme/ThemeProvider.tsx`）实现相同的机制——组件从不硬编码颜色，只使用 `var(--cm-*)`。

### 18. 故障排除

| 现象 | 可能原因 / 解决方法 |
|---|---|
| 后端因 JWT 密钥错误无法启动 | 在 `docker/.env` 或您的 Shell 环境中设置 `CHRONOSMESH_JWT_SECRET` |
| Rust 核心对可用性请求返回 400 错误 | 检查 `timezone` 字段是否为有效的 IANA 名称（例如 `Europe/Berlin`，而非 `CET`） |
| Web 应用的 WebSocket 始终无法连接 | 确认 `NEXT_PUBLIC_SCHEDULER_WS_URL` 与调度器暴露的端口和协议（`ws://` 还是 `wss://`）相匹配 |
| `go test` 无法获取模块 | 在受限网络环境中，请设置 `GOPROXY=direct GOSUMDB=off`（或可访问的模块代理） |
| 桌面端构建找不到 Qt6 | 将 `CMAKE_PREFIX_PATH` 设置为您的 Qt6 安装路径，例如 `-DCMAKE_PREFIX_PATH=C:/Qt/6.7.0/msvc2019_64` |
| Postgres 种子数据缺失 | `database/seed.sql` 仅在容器首次初始化时运行——删除 `chronosmesh_postgres_data` 卷以重新播种 |
