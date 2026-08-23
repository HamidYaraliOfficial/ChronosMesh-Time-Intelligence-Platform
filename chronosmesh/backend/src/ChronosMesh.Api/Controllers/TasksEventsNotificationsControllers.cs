using ChronosMesh.Application.DTOs;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ChronosMesh.Api.Controllers;

[Route("api/v1/tasks")]
public class TasksController : ChronosMeshControllerBase
{
    private readonly ITaskRepository _tasks;
    private readonly ISchedulerQueueClient _scheduler;

    public TasksController(ITaskRepository tasks, ISchedulerQueueClient scheduler)
    {
        _tasks = tasks;
        _scheduler = scheduler;
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var workspaceId = CurrentWorkspaceId ?? Guid.Empty;
        var task = new TaskItem
        {
            WorkspaceId = workspaceId,
            ProjectId = request.ProjectId,
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            DeadlineUtc = request.DeadlineUtc,
            Priority = (TaskPriority)request.Priority,
            Splittable = request.Splittable,
            MinChunkMinutes = request.MinChunkMinutes,
            AssignedToUserId = CurrentUserId,
            RequiredUserIds = request.RequiredUserIds,
        };
        await _tasks.AddAsync(task, ct);
        await _tasks.SaveChangesAsync(ct);

        if (task.DeadlineUtc is not null)
        {
            await _scheduler.EnqueueReminderAsync(workspaceId, CurrentUserId, $"Deadline approaching: {task.Title}", "deadline", ct);
        }

        return Ok(ToDto(task));
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> List(CancellationToken ct)
    {
        var tasks = await _tasks.GetByWorkspaceAsync(CurrentWorkspaceId ?? Guid.Empty, ct);
        return Ok(tasks.Select(ToDto).ToList());
    }

    private static TaskDto ToDto(TaskItem t) => new(t.Id, t.Title, t.DurationMinutes, t.DeadlineUtc, (int)t.Priority, t.Status.ToString(), t.Splittable);
}

[Route("api/v1/events")]
public class EventsController : ChronosMeshControllerBase
{
    private readonly IEventRepository _events;

    public EventsController(IEventRepository events)
    {
        _events = events;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEventRequest request, CancellationToken ct)
    {
        var freq = Enum.TryParse<RecurrenceFrequency>(request.RecurrenceFrequency, true, out var f) ? f : RecurrenceFrequency.None;
        var evt = new Event
        {
            CalendarId = request.CalendarId,
            Title = request.Title,
            Description = request.Description,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Timezone = request.Timezone,
            RecurrenceFrequency = freq,
            RecurrenceInterval = request.RecurrenceInterval,
            RecurrenceByWeekdayCsv = request.RecurrenceByWeekday is null ? null : string.Join(',', request.RecurrenceByWeekday),
            RecurrenceUntilUtc = request.RecurrenceUntilUtc,
            RecurrenceCount = request.RecurrenceCount,
        };
        await _events.AddAsync(evt, ct);
        await _events.SaveChangesAsync(ct);
        return Ok(new { id = evt.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid calendarId, [FromQuery] DateTime startUtc, [FromQuery] DateTime endUtc, CancellationToken ct)
    {
        var events = await _events.GetByCalendarAndRangeAsync(calendarId, startUtc, endUtc, ct);
        return Ok(events.Select(e => new { e.Id, e.Title, e.StartUtc, e.EndUtc, e.Timezone }));
    }
}

[Route("api/v1/notifications")]
public class NotificationsController : ChronosMeshControllerBase
{
    private readonly INotificationRepository _notifications;

    public NotificationsController(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> List([FromQuery] bool unreadOnly, CancellationToken ct)
    {
        var items = await _notifications.GetByUserAsync(CurrentUserId, unreadOnly, ct);
        return Ok(items.Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Body, n.IsRead, n.CreatedAtUtc)).ToList());
    }
}
