using ChronosMesh.Application.DTOs;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Domain.Enums;

namespace ChronosMesh.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}

public interface ITokenService
{
    (string token, DateTime expiresAtUtc) GenerateAccessToken(User user, Guid? workspaceId, RoleName? role);
    (string token, string tokenHash) GenerateRefreshToken();
    string HashToken(string token);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string ipAddress, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, string ipAddress, CancellationToken ct = default);
}

public interface IPermissionService
{
    bool HasPermission(RoleName role, PermissionResource resource, PermissionAction action);
    Task<RoleName?> GetUserRoleInWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default);
}

/// <summary>
/// Thin client over the Rust Secure Core / Time Engine HTTP microservice
/// (chronosmesh-core-server). All timezone-, DST-, and scheduling-sensitive
/// math is delegated here rather than re-implemented in C#.
/// </summary>
public interface ITimeEngineClient
{
    Task<AvailabilitySummaryDto> ComputeAvailabilitySummaryAsync(
        ScheduleDto schedule, IEnumerable<TimeIntervalDto> busy, DateTime nowUtc, CancellationToken ct = default);

    Task<List<TimeIntervalDto>> ComputeFreeIntervalsAsync(
        ScheduleDto schedule, IEnumerable<TimeIntervalDto> busy, TimeIntervalDto range, CancellationToken ct = default);
}

public interface ISchedulerQueueClient
{
    Task EnqueueReminderAsync(Guid workspaceId, Guid userId, string title, string kind, CancellationToken ct = default);
    Task EnqueueNotificationAsync(Guid workspaceId, Guid userId, NotificationType type, string title, string body, CancellationToken ct = default);
}

public interface IAuditLogger
{
    Task LogAsync(Guid workspaceId, Guid actorUserId, AuditAction action, string entityType, Guid entityId, object? metadata = null, CancellationToken ct = default);
}
