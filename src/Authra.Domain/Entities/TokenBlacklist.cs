namespace Authra.Domain.Entities;

/// <summary>
/// JWT blacklist for token revocation (MVP implementation).
/// Will be replaced by Redis in v1.1 for better performance.
/// Stores JWT IDs (jti) of revoked tokens until they expire.
/// </summary>
public class TokenBlacklist : Entity
{
    /// <summary>
    /// JWT ID (jti claim) of the blacklisted token.
    /// </summary>
    public string Jti { get; private set; } = string.Empty;

    /// <summary>
    /// When the original JWT expires (for cleanup).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the token was revoked/blacklisted.
    /// </summary>
    public DateTimeOffset RevokedAt { get; private set; }

    /// <summary>
    /// Reason for blacklisting: logout, password_change, admin, security.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// User who owned the blacklisted token (optional, for audit).
    /// </summary>
    public Guid? UserId { get; private set; }
    public User? User { get; private set; }

    /// <summary>
    /// Tenant context of the blacklisted token (optional, for audit).
    /// </summary>
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }

    private TokenBlacklist()
    {
        // EF Core constructor
    }

    public static TokenBlacklist Create(
        string jti,
        DateTimeOffset expiresAt,
        string reason,
        DateTimeOffset revokedAt,
        Guid? userId = null,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ValidateReason(reason);

        return new TokenBlacklist
        {
            Jti = jti,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            Reason = reason,
            UserId = userId,
            TenantId = tenantId
        };
    }

    public static TokenBlacklist CreateForLogout(
        string jti,
        DateTimeOffset expiresAt,
        DateTimeOffset revokedAt,
        Guid? userId = null,
        Guid? tenantId = null)
        => Create(jti, expiresAt, "logout", revokedAt, userId, tenantId);

    public static TokenBlacklist CreateForPasswordChange(
        string jti,
        DateTimeOffset expiresAt,
        DateTimeOffset revokedAt,
        Guid? userId = null,
        Guid? tenantId = null)
        => Create(jti, expiresAt, "password_change", revokedAt, userId, tenantId);

    public static TokenBlacklist CreateByAdmin(
        string jti,
        DateTimeOffset expiresAt,
        DateTimeOffset revokedAt,
        Guid? userId = null,
        Guid? tenantId = null)
        => Create(jti, expiresAt, "admin", revokedAt, userId, tenantId);

    public static TokenBlacklist CreateForSecurity(
        string jti,
        DateTimeOffset expiresAt,
        DateTimeOffset revokedAt,
        Guid? userId = null,
        Guid? tenantId = null)
        => Create(jti, expiresAt, "security", revokedAt, userId, tenantId);

    /// <summary>
    /// Checks if the blacklist entry is still needed (token hasn't expired yet).
    /// </summary>
    public bool IsStillNeeded(DateTimeOffset now) => now < ExpiresAt;

    private static void ValidateReason(string reason)
    {
        if (reason is not ("logout" or "password_change" or "admin" or "security"))
            throw new ArgumentException(
                $"Invalid reason '{reason}'. Supported: logout, password_change, admin, security.",
                nameof(reason));
    }
}
