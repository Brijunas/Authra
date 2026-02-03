namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Token pair containing access and refresh tokens.
/// </summary>
public record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Claims to include in the access token.
/// For user-only tokens (no tenant context), TenantId and TenantMemberId will be Guid.Empty.
/// </summary>
public record TokenClaims(
    Guid UserId,
    Guid TenantId,
    Guid TenantMemberId,
    IReadOnlyList<Guid> OrganizationIds,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    /// <summary>
    /// Returns true if this is a user-only token without tenant context.
    /// </summary>
    public bool IsUserOnly => TenantId == Guid.Empty;
};

/// <summary>
/// User-only access token (no tenant context) for users without tenants.
/// </summary>
public record UserOnlyAccessToken(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

/// <summary>
/// Service for JWT access token and opaque refresh token management.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a user-only access token (no tenant context).
    /// Used for users who have no tenant memberships yet.
    /// </summary>
    Task<UserOnlyAccessToken> GenerateUserOnlyAccessTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a new token pair (access token + refresh token).
    /// </summary>
    Task<TokenPair> GenerateTokenPairAsync(TokenClaims claims, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and returns the claims if valid.
    /// </summary>
    Task<TokenClaims?> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the token pair using a refresh token.
    /// Implements rotation with reuse detection.
    /// </summary>
    Task<TokenPair> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token and its entire family.
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all refresh tokens for a user in a specific tenant.
    /// </summary>
    Task RevokeAllUserTokensAsync(Guid userId, Guid tenantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Blacklists an access token (for immediate logout).
    /// </summary>
    Task BlacklistAccessTokenAsync(string accessToken, string reason, CancellationToken cancellationToken = default);
}
