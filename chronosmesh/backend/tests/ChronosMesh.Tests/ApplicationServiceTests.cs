using ChronosMesh.Application.Interfaces;
using ChronosMesh.Application.Services;
using ChronosMesh.Domain.Enums;
using Moq;
using Xunit;

namespace ChronosMesh.Tests;

public class PermissionServiceTests
{
    private readonly PermissionService _sut;

    public PermissionServiceTests()
    {
        var workspaces = new Mock<IWorkspaceRepository>();
        _sut = new PermissionService(workspaces.Object);
    }

    [Fact]
    public void Owner_has_every_permission_including_billing()
    {
        Assert.True(_sut.HasPermission(RoleName.Owner, PermissionResource.Workspace, PermissionAction.ManageBilling));
        Assert.True(_sut.HasPermission(RoleName.Owner, PermissionResource.Task, PermissionAction.Delete));
    }

    [Fact]
    public void Administrator_has_broad_access_but_not_billing()
    {
        Assert.False(_sut.HasPermission(RoleName.Administrator, PermissionResource.Workspace, PermissionAction.ManageBilling));
        Assert.True(_sut.HasPermission(RoleName.Administrator, PermissionResource.Task, PermissionAction.Delete));
    }

    [Fact]
    public void Manager_can_manage_team_members_and_crud_tasks()
    {
        Assert.True(_sut.HasPermission(RoleName.Manager, PermissionResource.Team, PermissionAction.ManageMembers));
        Assert.True(_sut.HasPermission(RoleName.Manager, PermissionResource.Task, PermissionAction.Delete));
    }

    [Fact]
    public void Member_can_create_and_update_but_not_delete_tasks()
    {
        Assert.True(_sut.HasPermission(RoleName.Member, PermissionResource.Task, PermissionAction.Create));
        Assert.True(_sut.HasPermission(RoleName.Member, PermissionResource.Task, PermissionAction.Update));
        Assert.False(_sut.HasPermission(RoleName.Member, PermissionResource.Task, PermissionAction.Delete));
    }

    [Fact]
    public void Viewer_can_only_read()
    {
        Assert.True(_sut.HasPermission(RoleName.Viewer, PermissionResource.Task, PermissionAction.Read));
        Assert.False(_sut.HasPermission(RoleName.Viewer, PermissionResource.Task, PermissionAction.Create));
        Assert.False(_sut.HasPermission(RoleName.Viewer, PermissionResource.Task, PermissionAction.Update));
    }
}

public class JwtTokenServiceTests
{
    [Fact]
    public void HashToken_is_deterministic_and_does_not_leak_the_raw_token()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "unit-test-super-secret-key-not-for-prod-000000",
                ["Jwt:Issuer"] = "chronosmesh-test",
                ["Jwt:Audience"] = "chronosmesh-test-clients",
            }).Build();

        var sut = new ChronosMesh.Infrastructure.Security.JwtTokenService(config);
        var (token, hash) = sut.GenerateRefreshToken();

        Assert.NotEqual(token, hash);
        Assert.Equal(hash, sut.HashToken(token));
        Assert.NotEqual(hash, sut.HashToken(token + "x"));
    }

    [Fact]
    public void GenerateAccessToken_embeds_expected_claims()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "unit-test-super-secret-key-not-for-prod-000000",
                ["Jwt:Issuer"] = "chronosmesh-test",
                ["Jwt:Audience"] = "chronosmesh-test-clients",
            }).Build();

        var sut = new ChronosMesh.Infrastructure.Security.JwtTokenService(config);
        var user = new ChronosMesh.Domain.Entities.User
        {
            Email = "test@chronosmesh.io",
            DisplayName = "Test User",
            PasswordHash = "irrelevant",
            Timezone = "Europe/Berlin",
            PreferredLanguage = "en",
        };

        var (token, expiresAtUtc) = sut.GenerateAccessToken(user, null, null);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAtUtc > DateTime.UtcNow);
        Assert.True(expiresAtUtc < DateTime.UtcNow.AddHours(1));
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_roundtrips_correctly()
    {
        var sut = new ChronosMesh.Infrastructure.Security.BCryptPasswordHasher();
        var hash = sut.Hash("Sup3rSecret!");
        Assert.True(sut.Verify("Sup3rSecret!", hash));
        Assert.False(sut.Verify("wrong-password", hash));
    }
}
