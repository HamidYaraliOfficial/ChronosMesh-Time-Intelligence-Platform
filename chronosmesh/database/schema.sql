-- ChronosMesh — PostgreSQL Schema
-- This file is the source of truth for the data model described in
-- README.markdown. In the running system it is applied via EF Core
-- migrations generated from backend/src/ChronosMesh.Domain (see
-- "dotnet ef migrations add InitialCreate"), but is also kept here as a
-- plain, reviewable SQL reference and for non-.NET tooling / manual setup.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ---------------------------------------------------------------------
-- Identity & Organization
-- ---------------------------------------------------------------------

CREATE TABLE organizations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(220) NOT NULL UNIQUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(320) NOT NULL UNIQUE,
    display_name VARCHAR(200) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    timezone VARCHAR(64) NOT NULL DEFAULT 'UTC',
    preferred_language VARCHAR(8) NOT NULL DEFAULT 'en', -- en | fa | zh
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at_utc TIMESTAMPTZ,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE workspaces (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    default_timezone VARCHAR(64) NOT NULL DEFAULT 'UTC',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE workspace_members (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL CHECK (role IN ('Owner','Administrator','Manager','Member','Viewer')),
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (workspace_id, user_id)
);

CREATE TABLE teams (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE team_members (
    team_id UUID NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    PRIMARY KEY (team_id, user_id)
);

CREATE TABLE role_permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    role VARCHAR(20) NOT NULL,
    resource VARCHAR(40) NOT NULL,
    action VARCHAR(20) NOT NULL,
    UNIQUE (role, resource, action)
);

-- ---------------------------------------------------------------------
-- Calendar & Events
-- ---------------------------------------------------------------------

CREATE TABLE calendars (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    owner_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    color VARCHAR(9) NOT NULL DEFAULT '#4F46E5',
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    calendar_id UUID NOT NULL REFERENCES calendars(id) ON DELETE CASCADE,
    title VARCHAR(300) NOT NULL,
    description TEXT,
    start_utc TIMESTAMPTZ NOT NULL,
    end_utc TIMESTAMPTZ NOT NULL,
    timezone VARCHAR(64) NOT NULL DEFAULT 'UTC',
    recurrence_frequency VARCHAR(10) NOT NULL DEFAULT 'None'
        CHECK (recurrence_frequency IN ('None','Daily','Weekly','Monthly','Yearly','Custom')),
    recurrence_interval INT NOT NULL DEFAULT 1,
    recurrence_by_weekday_csv VARCHAR(20),
    recurrence_until_utc TIMESTAMPTZ,
    recurrence_count INT,
    is_all_day BOOLEAN NOT NULL DEFAULT FALSE,
    project_id UUID,
    booking_id UUID,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (end_utc > start_utc)
);
CREATE INDEX idx_events_calendar_range ON events (calendar_id, start_utc, end_utc);

-- ---------------------------------------------------------------------
-- Projects & Tasks
-- ---------------------------------------------------------------------

CREATE TABLE projects (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    deadline_utc TIMESTAMPTZ,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE tasks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    project_id UUID REFERENCES projects(id) ON DELETE SET NULL,
    title VARCHAR(300) NOT NULL,
    description TEXT,
    duration_minutes INT NOT NULL CHECK (duration_minutes > 0),
    deadline_utc TIMESTAMPTZ,
    priority SMALLINT NOT NULL DEFAULT 2 CHECK (priority BETWEEN 1 AND 5),
    status VARCHAR(20) NOT NULL DEFAULT 'NotStarted'
        CHECK (status IN ('NotStarted','InProgress','Blocked','Completed','Cancelled')),
    splittable BOOLEAN NOT NULL DEFAULT FALSE,
    min_chunk_minutes INT NOT NULL DEFAULT 15,
    assigned_to_user_id UUID NOT NULL REFERENCES users(id),
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_tasks_workspace ON tasks (workspace_id);
CREATE INDEX idx_tasks_deadline ON tasks (deadline_utc);

CREATE TABLE task_required_users (
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    PRIMARY KEY (task_id, user_id)
);

-- ---------------------------------------------------------------------
-- Schedules / Working Hours / Availability
-- ---------------------------------------------------------------------

CREATE TABLE schedules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    timezone VARCHAR(64) NOT NULL DEFAULT 'UTC',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id)
);

CREATE TABLE working_days (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    schedule_id UUID NOT NULL REFERENCES schedules(id) ON DELETE CASCADE,
    weekday SMALLINT NOT NULL CHECK (weekday BETWEEN 0 AND 6), -- 0=Mon..6=Sun
    start_minute INT NOT NULL CHECK (start_minute BETWEEN 0 AND 1440),
    end_minute INT NOT NULL CHECK (end_minute BETWEEN 0 AND 1440),
    breaks_json JSONB NOT NULL DEFAULT '[]'
);

CREATE TABLE availability_overrides (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    start_utc TIMESTAMPTZ NOT NULL,
    end_utc TIMESTAMPTZ NOT NULL,
    is_unavailable BOOLEAN NOT NULL DEFAULT TRUE,
    reason VARCHAR(300),
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE holidays (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    holiday_date DATE NOT NULL,
    UNIQUE (workspace_id, holiday_date)
);

-- ---------------------------------------------------------------------
-- Booking
-- ---------------------------------------------------------------------

CREATE TABLE booking_services (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    owner_user_id UUID NOT NULL REFERENCES users(id),
    name VARCHAR(200) NOT NULL,
    duration_minutes INT NOT NULL CHECK (duration_minutes > 0),
    booking_slug VARCHAR(120) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE bookings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    booking_service_id UUID NOT NULL REFERENCES booking_services(id) ON DELETE CASCADE,
    guest_name VARCHAR(200) NOT NULL,
    guest_email VARCHAR(320) NOT NULL,
    start_utc TIMESTAMPTZ NOT NULL,
    end_utc TIMESTAMPTZ NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending','Confirmed','Cancelled','Completed','NoShow')),
    notes TEXT,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_bookings_service_range ON bookings (booking_service_id, start_utc, end_utc);

-- ---------------------------------------------------------------------
-- Notifications, Audit, Auth, Settings
-- ---------------------------------------------------------------------

CREATE TABLE notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type VARCHAR(30) NOT NULL
        CHECK (type IN ('TaskReminder','MeetingReminder','DeadlineWarning','Booking','ScheduleChange','TeamUpdate','SystemAlert')),
    title VARCHAR(300) NOT NULL,
    body TEXT NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at_utc TIMESTAMPTZ,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_notifications_user ON notifications (user_id, is_read);

CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    actor_user_id UUID NOT NULL REFERENCES users(id),
    action VARCHAR(30) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID NOT NULL,
    metadata_json JSONB,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_audit_logs_workspace ON audit_logs (workspace_id, created_at_utc DESC);

CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(128) NOT NULL UNIQUE,
    expires_at_utc TIMESTAMPTZ NOT NULL,
    revoked_at_utc TIMESTAMPTZ,
    replaced_by_token_hash VARCHAR(128),
    created_by_ip VARCHAR(64) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_refresh_tokens_user ON refresh_tokens (user_id);

CREATE TABLE settings (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    key VARCHAR(200) NOT NULL,
    value_json JSONB NOT NULL,
    UNIQUE (workspace_id, key)
);
