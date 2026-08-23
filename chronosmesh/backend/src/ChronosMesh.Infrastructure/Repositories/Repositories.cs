using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChronosMesh.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ChronosMeshDbContext _db;
    public UserRepository(ChronosMeshDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ChronosMeshDbContext _db;
    public RefreshTokenRepository(ChronosMeshDbContext db) => _db = db;

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await _db.RefreshTokens.AddAsync(token, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly ChronosMeshDbContext _db;
    public WorkspaceRepository(ChronosMeshDbContext db) => _db = db;

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<WorkspaceMember?> GetMembershipAsync(Guid userId, Guid workspaceId, CancellationToken ct = default) =>
        _db.WorkspaceMembers.FirstOrDefaultAsync(m => m.UserId == userId && m.WorkspaceId == workspaceId, ct);

    public async Task AddAsync(Workspace workspace, CancellationToken ct = default) =>
        await _db.Workspaces.AddAsync(workspace, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class ScheduleRepository : IScheduleRepository
{
    private readonly ChronosMeshDbContext _db;
    public ScheduleRepository(ChronosMeshDbContext db) => _db = db;

    public Task<ScheduleDefinition?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Schedules.Include(s => s.WorkingDays).FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task UpsertAsync(ScheduleDefinition schedule, CancellationToken ct = default)
    {
        var existing = await GetByUserIdAsync(schedule.UserId, ct);
        if (existing is null)
        {
            await _db.Schedules.AddAsync(schedule, ct);
        }
        else
        {
            existing.Timezone = schedule.Timezone;
            _db.WorkingDays.RemoveRange(existing.WorkingDays);
            foreach (var day in schedule.WorkingDays)
            {
                day.ScheduleId = existing.Id;
                existing.WorkingDays.Add(day);
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class TaskRepository : ITaskRepository
{
    private readonly ChronosMeshDbContext _db;
    public TaskRepository(ChronosMeshDbContext db) => _db = db;

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<TaskItem>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default) =>
        _db.Tasks.Where(t => t.WorkspaceId == workspaceId).OrderByDescending(t => t.Priority).ToListAsync(ct);

    public async Task AddAsync(TaskItem task, CancellationToken ct = default) =>
        await _db.Tasks.AddAsync(task, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class EventRepository : IEventRepository
{
    private readonly ChronosMeshDbContext _db;
    public EventRepository(ChronosMeshDbContext db) => _db = db;

    public Task<List<Event>> GetByCalendarAndRangeAsync(Guid calendarId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default) =>
        _db.Events
            .Where(e => e.CalendarId == calendarId && e.StartUtc < endUtc && e.EndUtc > startUtc)
            .OrderBy(e => e.StartUtc)
            .ToListAsync(ct);

    public async Task AddAsync(Event evt, CancellationToken ct = default) =>
        await _db.Events.AddAsync(evt, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class NotificationRepository : INotificationRepository
{
    private readonly ChronosMeshDbContext _db;
    public NotificationRepository(ChronosMeshDbContext db) => _db = db;

    public Task<List<NotificationEntity>> GetByUserAsync(Guid userId, bool unreadOnly, CancellationToken ct = default)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return query.OrderByDescending(n => n.CreatedAtUtc).Take(200).ToListAsync(ct);
    }

    public async Task AddAsync(NotificationEntity notification, CancellationToken ct = default) =>
        await _db.Notifications.AddAsync(notification, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
