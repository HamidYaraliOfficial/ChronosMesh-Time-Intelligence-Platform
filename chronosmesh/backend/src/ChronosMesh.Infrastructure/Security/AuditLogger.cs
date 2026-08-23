using System.Text.Json;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Domain.Enums;
using ChronosMesh.Infrastructure.Persistence;

namespace ChronosMesh.Infrastructure.Security;

/// <summary>
/// Writes an immutable audit trail entry for every sensitive action
/// (permission changes, role changes, deletions, logins). Consumed by the
/// Security screen in the Desktop Client / Web App and by the
/// AuditLogController for export.
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly ChronosMeshDbContext _db;

    public AuditLogger(ChronosMeshDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(Guid workspaceId, Guid actorUserId, AuditAction action, string entityType, Guid entityId, object? metadata = null, CancellationToken ct = default)
    {
        var entry = new AuditLogEntity
        {
            WorkspaceId = workspaceId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
        };
        await _db.AuditLogs.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }
}
