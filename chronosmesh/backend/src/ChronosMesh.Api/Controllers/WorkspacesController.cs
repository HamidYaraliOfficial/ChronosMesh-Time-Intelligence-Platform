using ChronosMesh.Application.DTOs;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ChronosMesh.Api.Controllers;

[Route("api/v1/workspaces")]
public class WorkspacesController : ChronosMeshControllerBase
{
    private readonly IWorkspaceRepository _workspaces;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public WorkspacesController(IWorkspaceRepository workspaces, IPermissionService permissions, IAuditLogger audit)
    {
        _workspaces = workspaces;
        _permissions = permissions;
        _audit = audit;
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create(CreateWorkspaceRequest request, CancellationToken ct)
    {
        var org = new Organization { Name = request.Name, Slug = Slugify(request.Name) };
        var workspace = new Workspace
        {
            Name = request.Name,
            DefaultTimezone = string.IsNullOrWhiteSpace(request.DefaultTimezone) ? "UTC" : request.DefaultTimezone,
            Organization = org,
            OrganizationId = org.Id,
        };
        workspace.Members.Add(new WorkspaceMember { UserId = CurrentUserId, Role = RoleName.Owner, WorkspaceId = workspace.Id });

        await _workspaces.AddAsync(workspace, ct);
        await _workspaces.SaveChangesAsync(ct);
        await _audit.LogAsync(workspace.Id, CurrentUserId, AuditAction.Created, nameof(Workspace), workspace.Id, ct: ct);

        return Ok(new WorkspaceDto(workspace.Id, workspace.Name, workspace.DefaultTimezone, workspace.OrganizationId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceDto>> Get(Guid id, CancellationToken ct)
    {
        var role = await _permissions.GetUserRoleInWorkspaceAsync(CurrentUserId, id, ct);
        if (role is null) return Forbid();
        if (!_permissions.HasPermission(role.Value, PermissionResource.Workspace, PermissionAction.Read)) return Forbid();

        var workspace = await _workspaces.GetByIdAsync(id, ct);
        if (workspace is null) return NotFound();
        return Ok(new WorkspaceDto(workspace.Id, workspace.Name, workspace.DefaultTimezone, workspace.OrganizationId));
    }

    private static string Slugify(string name) =>
        new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-') + "-" + Guid.NewGuid().ToString("N")[..6];
}
