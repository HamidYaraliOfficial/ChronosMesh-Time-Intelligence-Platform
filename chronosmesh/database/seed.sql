-- ChronosMesh — Seed Data
-- Populates the default Role -> Permission matrix mirrored in
-- backend/src/ChronosMesh.Application/Services/PermissionService.cs. Run
-- after schema.sql on a fresh database.

INSERT INTO role_permissions (role, resource, action)
SELECT role, resource, action FROM (VALUES
    -- Owner: every resource, every action (generated for brevity below via
    -- explicit rows covering the full product surface).
    ('Owner','Workspace','Read'), ('Owner','Workspace','Create'), ('Owner','Workspace','Update'),
    ('Owner','Workspace','Delete'), ('Owner','Workspace','ManageMembers'), ('Owner','Workspace','ManageBilling'),
    ('Owner','Workspace','ManageSettings'),
    ('Owner','Team','Read'), ('Owner','Team','Create'), ('Owner','Team','Update'), ('Owner','Team','Delete'), ('Owner','Team','ManageMembers'),
    ('Owner','Calendar','Read'), ('Owner','Calendar','Create'), ('Owner','Calendar','Update'), ('Owner','Calendar','Delete'),
    ('Owner','Event','Read'), ('Owner','Event','Create'), ('Owner','Event','Update'), ('Owner','Event','Delete'),
    ('Owner','Task','Read'), ('Owner','Task','Create'), ('Owner','Task','Update'), ('Owner','Task','Delete'),
    ('Owner','Project','Read'), ('Owner','Project','Create'), ('Owner','Project','Update'), ('Owner','Project','Delete'),
    ('Owner','Booking','Read'), ('Owner','Booking','Create'), ('Owner','Booking','Update'), ('Owner','Booking','Delete'),
    ('Owner','Schedule','Read'), ('Owner','Schedule','Create'), ('Owner','Schedule','Update'), ('Owner','Schedule','Delete'),
    ('Owner','Availability','Read'), ('Owner','Availability','Create'), ('Owner','Availability','Update'), ('Owner','Availability','Delete'),
    ('Owner','Notification','Read'), ('Owner','Notification','Create'), ('Owner','Notification','Update'), ('Owner','Notification','Delete'),
    ('Owner','AuditLog','Read'),
    ('Owner','Setting','Read'), ('Owner','Setting','Update'),

    -- Administrator: same as Owner except billing.
    ('Administrator','Workspace','Read'), ('Administrator','Workspace','Update'), ('Administrator','Workspace','ManageMembers'), ('Administrator','Workspace','ManageSettings'),
    ('Administrator','Team','Read'), ('Administrator','Team','Create'), ('Administrator','Team','Update'), ('Administrator','Team','Delete'), ('Administrator','Team','ManageMembers'),
    ('Administrator','Calendar','Read'), ('Administrator','Calendar','Create'), ('Administrator','Calendar','Update'), ('Administrator','Calendar','Delete'),
    ('Administrator','Event','Read'), ('Administrator','Event','Create'), ('Administrator','Event','Update'), ('Administrator','Event','Delete'),
    ('Administrator','Task','Read'), ('Administrator','Task','Create'), ('Administrator','Task','Update'), ('Administrator','Task','Delete'),
    ('Administrator','Project','Read'), ('Administrator','Project','Create'), ('Administrator','Project','Update'), ('Administrator','Project','Delete'),
    ('Administrator','Booking','Read'), ('Administrator','Booking','Create'), ('Administrator','Booking','Update'), ('Administrator','Booking','Delete'),
    ('Administrator','AuditLog','Read'),

    -- Manager: full CRUD on day-to-day resources, team member management.
    ('Manager','Team','Read'), ('Manager','Team','ManageMembers'),
    ('Manager','Calendar','Read'), ('Manager','Calendar','Create'), ('Manager','Calendar','Update'), ('Manager','Calendar','Delete'),
    ('Manager','Event','Read'), ('Manager','Event','Create'), ('Manager','Event','Update'), ('Manager','Event','Delete'),
    ('Manager','Task','Read'), ('Manager','Task','Create'), ('Manager','Task','Update'), ('Manager','Task','Delete'),
    ('Manager','Project','Read'), ('Manager','Project','Create'), ('Manager','Project','Update'), ('Manager','Project','Delete'),
    ('Manager','Booking','Read'), ('Manager','Booking','Create'), ('Manager','Booking','Update'), ('Manager','Booking','Delete'),
    ('Manager','Schedule','Read'), ('Manager','Schedule','Create'), ('Manager','Schedule','Update'), ('Manager','Schedule','Delete'),
    ('Manager','Availability','Read'),
    ('Manager','Notification','Read'), ('Manager','Notification','Create'),

    -- Member: create/update own work, cannot delete or manage the team.
    ('Member','Calendar','Read'), ('Member','Calendar','Create'), ('Member','Calendar','Update'),
    ('Member','Event','Read'), ('Member','Event','Create'), ('Member','Event','Update'),
    ('Member','Task','Read'), ('Member','Task','Create'), ('Member','Task','Update'),
    ('Member','Project','Read'), ('Member','Project','Create'), ('Member','Project','Update'),
    ('Member','Booking','Read'), ('Member','Booking','Create'), ('Member','Booking','Update'),
    ('Member','Schedule','Read'), ('Member','Schedule','Create'), ('Member','Schedule','Update'),
    ('Member','Availability','Read'),
    ('Member','Notification','Read'),

    -- Viewer: read-only across the board.
    ('Viewer','Workspace','Read'), ('Viewer','Team','Read'), ('Viewer','Calendar','Read'), ('Viewer','Event','Read'),
    ('Viewer','Task','Read'), ('Viewer','Project','Read'), ('Viewer','Booking','Read'), ('Viewer','Schedule','Read'),
    ('Viewer','Availability','Read'), ('Viewer','Notification','Read')
) AS seed(role, resource, action)
ON CONFLICT (role, resource, action) DO NOTHING;

-- A ready-to-explore demo organization/workspace (safe to delete before
-- going to production).
INSERT INTO organizations (id, name, slug)
VALUES ('00000000-0000-0000-0000-000000000001', 'ChronosMesh Demo Org', 'chronosmesh-demo')
ON CONFLICT DO NOTHING;

INSERT INTO workspaces (id, organization_id, name, default_timezone)
VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 'Demo Workspace', 'UTC')
ON CONFLICT DO NOTHING;
