using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChronosMesh.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ChronosMeshControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedAccessException("Missing subject claim."));

    protected Guid? CurrentWorkspaceId
    {
        get
        {
            var value = User.FindFirstValue("workspace_id");
            return value is null ? null : Guid.Parse(value);
        }
    }
}
