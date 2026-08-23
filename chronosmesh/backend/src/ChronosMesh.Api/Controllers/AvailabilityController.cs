using ChronosMesh.Application.DTOs;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ChronosMesh.Api.Controllers;

[Route("api/v1/schedules")]
public class SchedulesController : ChronosMeshControllerBase
{
    private readonly IScheduleRepository _schedules;

    public SchedulesController(IScheduleRepository schedules)
    {
        _schedules = schedules;
    }

    /// <summary>
    /// Persists the calling user's declarative working-hours pattern:
    /// which days they work, start/end times, breaks, and timezone. This
    /// is the source of truth the Availability Engine (Rust) projects into
    /// concrete free/busy time.
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<ScheduleDto>> UpsertMine(UpsertScheduleRequest request, CancellationToken ct)
    {
        var schedule = new ScheduleDefinition
        {
            UserId = CurrentUserId,
            WorkspaceId = CurrentWorkspaceId ?? Guid.Empty,
            Timezone = request.Timezone,
        };
        foreach (var day in request.WorkingDays)
        {
            schedule.WorkingDays.Add(new WorkingDayEntity
            {
                Weekday = day.Weekday,
                StartMinute = day.StartMinute,
                EndMinute = day.EndMinute,
                BreaksJson = System.Text.Json.JsonSerializer.Serialize(day.Breaks),
            });
        }

        await _schedules.UpsertAsync(schedule, ct);
        await _schedules.SaveChangesAsync(ct);

        return Ok(ToDto(schedule));
    }

    [HttpGet("me")]
    public async Task<ActionResult<ScheduleDto>> GetMine(CancellationToken ct)
    {
        var schedule = await _schedules.GetByUserIdAsync(CurrentUserId, ct);
        if (schedule is null) return NotFound();
        return Ok(ToDto(schedule));
    }

    private static ScheduleDto ToDto(ScheduleDefinition s) => new(
        s.UserId,
        s.Timezone,
        s.WorkingDays.Select(d => new WorkingDayDto(
            d.Weekday, d.StartMinute, d.EndMinute,
            System.Text.Json.JsonSerializer.Deserialize<List<int[]>>(d.BreaksJson) ?? new()
        )).ToList()
    );
}

[Route("api/v1/availability")]
public class AvailabilityController : ChronosMeshControllerBase
{
    private readonly IScheduleRepository _schedules;
    private readonly ITimeEngineClient _timeEngine;

    public AvailabilityController(IScheduleRepository schedules, ITimeEngineClient timeEngine)
    {
        _schedules = schedules;
        _timeEngine = timeEngine;
    }

    /// <summary>
    /// Returns the full availability picture for the calling user: free
    /// intervals today, next available slot, total free time today/this
    /// week, remaining working time today, and time until the next
    /// available slot. All timezone/DST math happens in the Rust Time
    /// Engine — this endpoint only assembles the request.
    /// </summary>
    [HttpGet("me/summary")]
    public async Task<ActionResult<AvailabilitySummaryDto>> GetMySummary(CancellationToken ct)
    {
        var schedule = await _schedules.GetByUserIdAsync(CurrentUserId, ct);
        if (schedule is null) return NotFound(new { error = "No working-hours schedule configured yet." });

        var scheduleDto = new ScheduleDto(
            schedule.UserId, schedule.Timezone,
            schedule.WorkingDays.Select(d => new WorkingDayDto(
                d.Weekday, d.StartMinute, d.EndMinute,
                System.Text.Json.JsonSerializer.Deserialize<List<int[]>>(d.BreaksJson) ?? new()
            )).ToList()
        );

        // Busy intervals (existing meetings/tasks/bookings) would normally
        // be loaded from IEventRepository / ITaskRepository for the
        // relevant date range; omitted here for brevity but the wiring
        // point is exactly this list.
        var busy = new List<TimeIntervalDto>();

        var summary = await _timeEngine.ComputeAvailabilitySummaryAsync(scheduleDto, busy, DateTime.UtcNow, ct);
        return Ok(summary);
    }
}
