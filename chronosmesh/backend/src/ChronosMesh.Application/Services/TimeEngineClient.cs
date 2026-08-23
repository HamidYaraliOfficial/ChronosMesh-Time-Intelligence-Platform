using System.Net.Http.Json;
using ChronosMesh.Application.DTOs;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Enums;

namespace ChronosMesh.Application.Services;

/// <summary>
/// HTTP client for the chronosmesh-core-server (Rust Secure Core / Time
/// Engine). Every timezone/DST-sensitive calculation is delegated here
/// rather than duplicated in C#.
/// </summary>
public class TimeEngineClient : ITimeEngineClient
{
    private readonly HttpClient _http;

    public TimeEngineClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AvailabilitySummaryDto> ComputeAvailabilitySummaryAsync(
        ScheduleDto schedule, IEnumerable<TimeIntervalDto> busy, DateTime nowUtc, CancellationToken ct = default)
    {
        var payload = new
        {
            working_hours = ToWorkingHoursPayload(schedule),
            busy = busy.Select(ToIntervalPayload),
            now = nowUtc,
        };
        var response = await _http.PostAsJsonAsync("/v1/availability/summary", payload, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CoreAvailabilitySummary>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from time engine.");
        return MapSummary(result);
    }

    public async Task<List<TimeIntervalDto>> ComputeFreeIntervalsAsync(
        ScheduleDto schedule, IEnumerable<TimeIntervalDto> busy, TimeIntervalDto range, CancellationToken ct = default)
    {
        var payload = new
        {
            working_hours = ToWorkingHoursPayload(schedule),
            busy = busy.Select(ToIntervalPayload),
            range = ToIntervalPayload(range),
        };
        var response = await _http.PostAsJsonAsync("/v1/availability/compute", payload, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<CoreInterval>>(cancellationToken: ct) ?? new();
        return result.Select(i => new TimeIntervalDto(i.start, i.end)).ToList();
    }

    private static object ToWorkingHoursPayload(ScheduleDto schedule) => new
    {
        timezone = schedule.Timezone,
        days = schedule.WorkingDays.Select(d => new
        {
            weekday = d.Weekday,
            start_minute = d.StartMinute,
            end_minute = d.EndMinute,
            breaks = d.Breaks.Select(b => new[] { b[0], b[1] }),
        }),
        holidays = Array.Empty<string>(),
    };

    private static object ToIntervalPayload(TimeIntervalDto i) => new { start = i.StartUtc, end = i.EndUtc };

    private static AvailabilitySummaryDto MapSummary(CoreAvailabilitySummary s) => new(
        s.free_intervals.Select(i => new TimeIntervalDto(i.start, i.end)).ToList(),
        s.next_available_slot is null ? null : new TimeIntervalDto(s.next_available_slot.start, s.next_available_slot.end),
        s.total_free_minutes_today,
        s.total_free_minutes_week,
        s.remaining_working_minutes_today,
        s.minutes_until_next_available
    );

    // Wire-format mirrors of the Rust `AvailabilitySummary` / `TimeInterval`
    // JSON shapes (snake_case, as produced by serde).
    private record CoreInterval(DateTime start, DateTime end);
    private record CoreAvailabilitySummary(
        List<CoreInterval> free_intervals,
        CoreInterval? next_available_slot,
        long total_free_minutes_today,
        long total_free_minutes_week,
        long remaining_working_minutes_today,
        long? minutes_until_next_available
    );
}

/// <summary>
/// HTTP client for the Go Scheduler Engine's job-submission API. Used to
/// enqueue reminders and notifications instead of processing them inline
/// on the request thread.
/// </summary>
public class SchedulerQueueClient : ISchedulerQueueClient
{
    private readonly HttpClient _http;

    public SchedulerQueueClient(HttpClient http)
    {
        _http = http;
    }

    public async Task EnqueueReminderAsync(Guid workspaceId, Guid userId, string title, string kind, CancellationToken ct = default)
    {
        var payload = new
        {
            type = "reminder",
            priority = 1, // High
            workspace_id = workspaceId.ToString(),
            payload = new { user_id = userId.ToString(), title, kind },
        };
        var response = await _http.PostAsJsonAsync("/v1/jobs", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnqueueNotificationAsync(Guid workspaceId, Guid userId, NotificationType type, string title, string body, CancellationToken ct = default)
    {
        var payload = new
        {
            type = "push_notify",
            priority = 2, // Normal
            workspace_id = workspaceId.ToString(),
            payload = new { user_id = userId.ToString(), title, body, notification_type = type.ToString() },
        };
        var response = await _http.PostAsJsonAsync("/v1/jobs", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
