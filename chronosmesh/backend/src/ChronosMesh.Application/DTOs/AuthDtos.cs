namespace ChronosMesh.Application.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName, string Timezone, string PreferredLanguage);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, string Timezone, string PreferredLanguage);

public record WorkspaceDto(Guid Id, string Name, string DefaultTimezone, Guid OrganizationId);
public record CreateWorkspaceRequest(string Name, string DefaultTimezone);

public record WorkingDayDto(byte Weekday, int StartMinute, int EndMinute, List<int[]> Breaks);
public record ScheduleDto(Guid UserId, string Timezone, List<WorkingDayDto> WorkingDays);
public record UpsertScheduleRequest(string Timezone, List<WorkingDayDto> WorkingDays);

public record TimeIntervalDto(DateTime StartUtc, DateTime EndUtc);

public record AvailabilitySummaryDto(
    List<TimeIntervalDto> FreeIntervals,
    TimeIntervalDto? NextAvailableSlot,
    long TotalFreeMinutesToday,
    long TotalFreeMinutesWeek,
    long RemainingWorkingMinutesToday,
    long? MinutesUntilNextAvailable
);

public record CreateTaskRequest(
    string Title,
    string? Description,
    int DurationMinutes,
    DateTime? DeadlineUtc,
    int Priority,
    Guid? ProjectId,
    bool Splittable,
    int MinChunkMinutes,
    List<Guid> RequiredUserIds
);

public record TaskDto(
    Guid Id,
    string Title,
    int DurationMinutes,
    DateTime? DeadlineUtc,
    int Priority,
    string Status,
    bool Splittable
);

public record CreateEventRequest(
    Guid CalendarId,
    string Title,
    string? Description,
    DateTime StartUtc,
    DateTime EndUtc,
    string Timezone,
    string RecurrenceFrequency,
    int RecurrenceInterval,
    List<byte>? RecurrenceByWeekday,
    DateTime? RecurrenceUntilUtc,
    int? RecurrenceCount
);

public record BookingServiceDto(Guid Id, string Name, int DurationMinutes, string BookingSlug, bool IsActive);
public record CreateBookingServiceRequest(string Name, int DurationMinutes);
public record CreateBookingRequest(string GuestName, string GuestEmail, DateTime StartUtc, string? Notes);

public record NotificationDto(Guid Id, string Type, string Title, string Body, bool IsRead, DateTime CreatedAtUtc);

public record AnalyticsSummaryDto(
    long WorkingMinutes,
    long MeetingMinutes,
    long FocusMinutes,
    long FreeMinutes,
    int TasksCompleted,
    int TasksTotal,
    long OvertimeMinutes,
    double ProductivityScore
);

public record SearchResultDto(string EntityType, Guid Id, string Title, string? Subtitle);
