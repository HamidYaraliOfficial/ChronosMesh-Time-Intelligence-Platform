using ChronosMesh.Application.DTOs;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Domain.Entities;

namespace ChronosMesh.Application.Services;

/// <summary>
/// Handles registration, login, and secure access/refresh-token rotation.
/// Refresh tokens are single-use: on every refresh a new token is issued
/// and the old one is revoked and linked via <see cref="RefreshToken.ReplacedByTokenHash"/>,
/// so a stolen, already-used refresh token is immediately detectable as
/// reuse and can trigger session revocation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public AuthService(IUserRepository users, IRefreshTokenRepository refreshTokens, IPasswordHasher hasher, ITokenService tokens)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName,
            PasswordHash = _hasher.Hash(request.Password),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone,
            PreferredLanguage = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "en" : request.PreferredLanguage,
        };
        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ipAddress, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _users.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ipAddress, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string ipAddress, CancellationToken ct = default)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _refreshTokens.GetByTokenHashAsync(hash, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!stored.IsActive)
        {
            // Reuse of an already-revoked/expired token: treat as a
            // possible compromise. In production this should cascade-revoke
            // every active token for the user.
            throw new UnauthorizedAccessException("Refresh token is no longer valid.");
        }

        var user = await _users.GetByIdAsync(stored.UserId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var (newRefreshToken, newHash) = _tokens.GenerateRefreshToken();
        stored.RevokedAtUtc = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newHash;

        var newRecord = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedByIp = ipAddress,
        };
        await _refreshTokens.AddAsync(newRecord, ct);
        await _refreshTokens.SaveChangesAsync(ct);

        var (accessToken, accessExpiry) = _tokens.GenerateAccessToken(user, workspaceId: null, role: null);
        return new AuthResponse(accessToken, newRefreshToken, accessExpiry, ToDto(user));
    }

    public async Task RevokeAsync(string refreshToken, string ipAddress, CancellationToken ct = default)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _refreshTokens.GetByTokenHashAsync(hash, ct);
        if (stored is null || !stored.IsActive) return;
        stored.RevokedAtUtc = DateTime.UtcNow;
        await _refreshTokens.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string ipAddress, CancellationToken ct)
    {
        var (accessToken, accessExpiry) = _tokens.GenerateAccessToken(user, workspaceId: null, role: null);
        var (refreshToken, refreshHash) = _tokens.GenerateRefreshToken();

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedByIp = ipAddress,
        }, ct);
        await _refreshTokens.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, accessExpiry, ToDto(user));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Timezone, u.PreferredLanguage);
}
