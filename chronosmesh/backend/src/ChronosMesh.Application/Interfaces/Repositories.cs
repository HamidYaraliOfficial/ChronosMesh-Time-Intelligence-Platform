using ChronosMesh.Domain.Entities;

namespace ChronosMesh.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkspaceMember?> GetMembershipAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);
    Task AddAsync(Workspace workspace, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IScheduleRepository
{
    Task<ScheduleDefinition?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(ScheduleDefinition schedule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TaskItem>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IEventRepository
{
    Task<List<Event>> GetByCalendarAndRangeAsync(Guid calendarId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task AddAsync(Event evt, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface INotificationRepository
{
    Task<List<NotificationEntity>> GetByUserAsync(Guid userId, bool unreadOnly, CancellationToken ct = default);
    Task AddAsync(NotificationEntity notification, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
