namespace ChronosMesh.Domain.Enums;

public enum RoleName
{
    Owner,
    Administrator,
    Manager,
    Member,
    Viewer
}

public enum PermissionAction
{
    Read,
    Create,
    Update,
    Delete,
    ManageMembers,
    ManageBilling,
    ManageSettings
}

public enum PermissionResource
{
    Workspace,
    Team,
    Calendar,
    Event,
    Task,
    Project,
    Booking,
    Schedule,
    Availability,
    Notification,
    AuditLog,
    Setting
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
    Critical = 5
}

public enum TaskStatus
{
    NotStarted,
    InProgress,
    Blocked,
    Completed,
    Cancelled
}

public enum RecurrenceFrequency
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Custom
}

public enum NotificationType
{
    TaskReminder,
    MeetingReminder,
    DeadlineWarning,
    Booking,
    ScheduleChange,
    TeamUpdate,
    SystemAlert
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    LoggedIn,
    LoggedOut,
    PermissionChanged,
    RoleChanged
}
