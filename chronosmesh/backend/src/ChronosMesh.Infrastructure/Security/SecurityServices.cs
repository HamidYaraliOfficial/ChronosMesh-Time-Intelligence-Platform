using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;
using ChronosMesh.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ChronosMesh.Infrastructure.Security;

/// <summary>
/// BCrypt-based password hashing. Passwords are never stored or logged in
/// plain text anywhere in the system; only this hash leaves memory.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext, WorkFactor);

    public bool Verify(string plaintext, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plaintext, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

/// <summary>
/// Issues short-lived JWT access tokens (signed HS256) and opaque,
/// cryptographically random refresh tokens. Refresh tokens are stored
/// server-side only as a SHA-256 hash — the raw token is returned to the
/// client exactly once and can never be recovered from the database.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string token, DateTime expiresAtUtc) GenerateAccessToken(User user, Guid? workspaceId, RoleName? role)
    {
        var secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer = _config["Jwt:Issuer"] ?? "chronosmesh";
        var audience = _config["Jwt:Audience"] ?? "chronosmesh-clients";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("display_name", user.DisplayName),
            new("timezone", user.Timezone),
            new("lang", user.PreferredLanguage),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (workspaceId is not null) claims.Add(new Claim("workspace_id", workspaceId.Value.ToString()));
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role.Value.ToString()));

        var expires = DateTime.UtcNow.Add(AccessTokenLifetime);
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string token, string tokenHash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return (token, HashToken(token));
    }

    public string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
