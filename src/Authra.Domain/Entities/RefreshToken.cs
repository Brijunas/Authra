using System.Net;

namespace Authra.Domain.Entities;

/// <summary>
/// Refresh token storage with rotation tracking.
/// Tokens are stored as SHA-256 hashes - never in plaintext.
/// Supports token family tracking for reuse detection.
/// </summary>
public class RefreshToken : Entity
{
    /// <summary>
    /// Default sliding expiration (30 days).
    /// </summary>
    public static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromDays(30);

    /// <summary>
    /// Default absolute expiration (90 days from initial issue).
    /// </summary>
    public static readonly TimeSpan DefaultAbsoluteExpiration = TimeSpan.FromDays(90);

    /// <summary>
    /// SHA-256 hash of the token. Never store plaintext tokens.
    /// </summary>
    public byte[] TokenHash { get; private set; } = null!;

    /// <summary>
    /// User who owns this token (global identity).
    /// </summary>
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// Tenant context for this token.
    /// </summary>
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Tenant member this token is associated with.
    /// </summary>
    public Guid TenantMemberId { get; private set; }
    public TenantMember TenantMember { get; private set; } = null!;

    /// <summary>
    /// Token family ID - shared across rotation chain for reuse detection.
    /// </summary>
    public Guid FamilyId { get; private set; }

    /// <summary>
    /// Generation number - increments on each rotation within the family.
    /// </summary>
    public int Generation { get; private set; } = 1;

    /// <summary>
    /// Optional client device identifier.
    /// </summary>
    public string? DeviceId { get; private set; }

    /// <summary>
    /// IP address when token was issued (for audit).
    /// </summary>
    public IPAddress? IpAddress { get; private set; }

    /// <summary>
    /// When the token was issued.
    /// </summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>
    /// Sliding expiration - when the token expires if not refreshed.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the token was revoked. NULL if still valid.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Reason for revocation: logout, rotation, reuse_detected, admin, password_change.
    /// </summary>
    public string? RevokedReason { get; private set; }

    /// <summary>
    /// Absolute expiration - maximum lifetime regardless of refresh.
    /// </summary>
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    private RefreshToken()
    {
        // EF Core constructor
    }

    public static RefreshToken Create(
        byte[] tokenHash,
        Guid userId,
        Guid tenantId,
        Guid tenantMemberId,
        DateTimeOffset expiresAt,
        DateTimeOffset absoluteExpiresAt,
        Guid? familyId = null,
        int generation = 1,
        string? deviceId = null,
        IPAddress? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        return new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = userId,
            TenantId = tenantId,
            TenantMemberId = tenantMemberId,
            FamilyId = familyId ?? Guid.NewGuid(),
            Generation = generation,
            DeviceId = deviceId,
            IpAddress = ipAddress,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt
        };
    }

    /// <summary>
    /// Creates a rotated token in the same family with incremented generation.
    /// </summary>
    public static RefreshToken CreateRotated(
        byte[] tokenHash,
        RefreshToken previousToken,
        DateTimeOffset expiresAt,
        IPAddress? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentNullException.ThrowIfNull(previousToken);

        return new RefreshToken
        {
            TokenHash = tokenHash,
            UserId = previousToken.UserId,
            TenantId = previousToken.TenantId,
            TenantMemberId = previousToken.TenantMemberId,
            FamilyId = previousToken.FamilyId,
            Generation = previousToken.Generation + 1,
            DeviceId = previousToken.DeviceId,
            IpAddress = ipAddress,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = previousToken.AbsoluteExpiresAt
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt || now >= AbsoluteExpiresAt;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsValid(DateTimeOffset now) => !IsExpired(now) && !IsRevoked;

    public void Revoke(string reason, DateTimeOffset revokedAt)
    {
        ValidateRevokeReason(reason);

        if (RevokedAt.HasValue)
            return; // Already revoked

        RevokedAt = revokedAt;
        RevokedReason = reason;
    }

    public void RevokeForLogout(DateTimeOffset revokedAt) => Revoke("logout", revokedAt);
    public void RevokeForRotation(DateTimeOffset revokedAt) => Revoke("rotation", revokedAt);
    public void RevokeForReuseDetected(DateTimeOffset revokedAt) => Revoke("reuse_detected", revokedAt);
    public void RevokeForPasswordChange(DateTimeOffset revokedAt) => Revoke("password_change", revokedAt);
    public void RevokeByAdmin(DateTimeOffset revokedAt) => Revoke("admin", revokedAt);

    private static void ValidateRevokeReason(string reason)
    {
        if (reason is not ("logout" or "rotation" or "reuse_detected" or "admin" or "password_change"))
            throw new ArgumentException(
                $"Invalid revoke reason '{reason}'. Supported: logout, rotation, reuse_detected, admin, password_change.",
                nameof(reason));
    }
}
