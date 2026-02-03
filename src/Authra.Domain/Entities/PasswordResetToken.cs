using System.Net;

namespace Authra.Domain.Entities;

/// <summary>
/// Password reset token for account recovery.
/// Global entity - tokens are hashed (SHA-256) and never stored in plaintext.
/// </summary>
public class PasswordResetToken : Entity
{
    /// <summary>
    /// Default token expiration time (1 hour).
    /// </summary>
    public static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(1);

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// SHA-256 hash of the token. Never store plaintext tokens.
    /// </summary>
    public byte[] TokenHash { get; private set; } = null!;

    /// <summary>
    /// When the token expires (typically 1 hour from creation).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the token was used. NULL until used.
    /// </summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// IP address that requested the reset (for audit).
    /// </summary>
    public IPAddress? CreatedByIp { get; private set; }

    private PasswordResetToken()
    {
        // EF Core constructor
    }

    public static PasswordResetToken Create(
        Guid userId,
        byte[] tokenHash,
        DateTimeOffset expiresAt,
        IPAddress? createdByIp = null)
    {
        return new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp
        };
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;

    public bool IsValid(DateTimeOffset now) => !IsExpired(now) && !IsUsed;

    public void MarkAsUsed(DateTimeOffset usedAt)
    {
        if (UsedAt.HasValue)
            throw new InvalidOperationException("Token has already been used");

        UsedAt = usedAt;
    }
}
