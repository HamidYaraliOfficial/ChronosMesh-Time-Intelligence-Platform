using ChronosMesh.Domain.Enums;

namespace ChronosMesh.Domain.Entities;

public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public class User : AuditableEntity
{
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Timezone { get; set; } = "UTC";
    public string PreferredLanguage { get; set; } = "en"; // en | fa | zh
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class Organization : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}

public class Workspace : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = default!;
    public string DefaultTimezone { get; set; } = "UTC";

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<CalendarEntity> Calendars { get; set; } = new List<CalendarEntity>();
}

public class WorkspaceMember : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public RoleName Role { get; set; } = RoleName.Member;
}

public class Team : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }
    public string Name { get; set; } = default!;
    public ICollection<Guid> MemberUserIds { get; set; } = new List<Guid>();
}

public class RolePermission : AuditableEntity
{
    public RoleName Role { get; set; }
    public PermissionResource Resource { get; set; }
    public PermissionAction Action { get; set; }
}

public class CalendarEntity : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = default!;
    public string Color { get; set; } = "#4F46E5";
    public bool IsPrimary { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public class Event : AuditableEntity
{
    public Guid CalendarId { get; set; }
    public CalendarEntity? Calendar { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Timezone { get; set; } = "UTC";
    public RecurrenceFrequency RecurrenceFrequency { get; set; } = RecurrenceFrequency.None;
    public int RecurrenceInterval { get; set; } = 1;
    public string? RecurrenceByWeekdayCsv { get; set; }
    public DateTime? RecurrenceUntilUtc { get; set; }
    public int? RecurrenceCount { get; set; }
    public bool IsAllDay { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? BookingId { get; set; }
}

public class Project : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public class TaskItem : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.NotStarted;
    public bool Splittable { get; set; }
    public int MinChunkMinutes { get; set; } = 15;
    public Guid AssignedToUserId { get; set; }
    public ICollection<Guid> RequiredUserIds { get; set; } = new List<Guid>();
}

public class ScheduleDefinition : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Timezone { get; set; } = "UTC";
    public ICollection<WorkingDayEntity> WorkingDays { get; set; } = new List<WorkingDayEntity>();
}

public class WorkingDayEntity : AuditableEntity
{
    public Guid ScheduleId { get; set; }
    public byte Weekday { get; set; } // 0=Mon..6=Sun
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string BreaksJson { get; set; } = "[]"; // [[start,end], ...]
}

public class AvailabilityOverride : AuditableEntity
{
    public Guid UserId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsUnavailable { get; set; } = true;
    public string? Reason { get; set; }
}

public class HolidayEntity : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly Date { get; set; }
}

public class BookingService : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = default!;
    public int DurationMinutes { get; set; }
    public string BookingSlug { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public class BookingEntity : AuditableEntity
{
    public Guid BookingServiceId { get; set; }
    public string GuestName { get; set; } = default!;
    public string GuestEmail { get; set; } = default!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Notes { get; set; }
}

public class NotificationEntity : AuditableEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public class AuditLogEntity : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public Guid ActorUserId { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string? MetadataJson { get; set; }
}

public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string CreatedByIp { get; set; } = default!;

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

public class SettingEntity : AuditableEntity
{
    public Guid WorkspaceId { get; set; }
    public string Key { get; set; } = default!;
    public string ValueJson { get; set; } = default!;
}
