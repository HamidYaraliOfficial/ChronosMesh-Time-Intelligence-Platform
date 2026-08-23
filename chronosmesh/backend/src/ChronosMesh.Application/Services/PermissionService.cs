using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Enums;

namespace ChronosMesh.Application.Services;

/// <summary>
/// Central RBAC decision point. The default permission matrix below is the
/// seed data written to the database on first migration (see
/// database/seed.sql); this in-memory copy is also used as a fast-path so
/// hot-path permission checks don't require a database round trip.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IWorkspaceRepository _workspaces;

    private static readonly Dictionary<RoleName, HashSet<(PermissionResource, PermissionAction)>> Matrix = BuildMatrix();

    public PermissionService(IWorkspaceRepository workspaces)
    {
        _workspaces = workspaces;
    }

    public bool HasPermission(RoleName role, PermissionResource resource, PermissionAction action)
    {
        return Matrix.TryGetValue(role, out var granted) && granted.Contains((resource, action));
    }

    public async Task<RoleName?> GetUserRoleInWorkspaceAsync(Guid userId, Guid workspaceId, CancellationToken ct = default)
    {
        var membership = await _workspaces.GetMembershipAsync(userId, workspaceId, ct);
        return membership?.Role;
    }

    private static Dictionary<RoleName, HashSet<(PermissionResource, PermissionAction)>> BuildMatrix()
    {
        var resources = Enum.GetValues<PermissionResource>();
        var actions = Enum.GetValues<PermissionAction>();

        var owner = new HashSet<(PermissionResource, PermissionAction)>();
        foreach (var r in resources)
            foreach (var a in actions)
                owner.Add((r, a)); // Owner: full access to everything.

        var admin = new HashSet<(PermissionResource, PermissionAction)>(owner);
        admin.Remove((PermissionResource.Workspace, PermissionAction.ManageBilling)); // billing stays owner-only.

        var manager = new HashSet<(PermissionResource, PermissionAction)>();
        foreach (var r in new[] { PermissionResource.Team, PermissionResource.Calendar, PermissionResource.Event, PermissionResource.Task, PermissionResource.Project, PermissionResource.Booking, PermissionResource.Schedule, PermissionResource.Availability, PermissionResource.Notification })
        {
            manager.Add((r, PermissionAction.Read));
            manager.Add((r, PermissionAction.Create));
            manager.Add((r, PermissionAction.Update));
            manager.Add((r, PermissionAction.Delete));
        }
        manager.Add((PermissionResource.Team, PermissionAction.ManageMembers));

        var member = new HashSet<(PermissionResource, PermissionAction)>();
        foreach (var r in new[] { PermissionResource.Calendar, PermissionResource.Event, PermissionResource.Task, PermissionResource.Project, PermissionResource.Booking, PermissionResource.Schedule, PermissionResource.Availability, PermissionResource.Notification })
        {
            member.Add((r, PermissionAction.Read));
            member.Add((r, PermissionAction.Create));
            member.Add((r, PermissionAction.Update));
        }

        var viewer = new HashSet<(PermissionResource, PermissionAction)>();
        foreach (var r in resources)
            viewer.Add((r, PermissionAction.Read));

        return new Dictionary<RoleName, HashSet<(PermissionResource, PermissionAction)>>
        {
            [RoleName.Owner] = owner,
            [RoleName.Administrator] = admin,
            [RoleName.Manager] = manager,
            [RoleName.Member] = member,
            [RoleName.Viewer] = viewer,
        };
    }
}
