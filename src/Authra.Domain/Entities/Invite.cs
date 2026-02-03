namespace Authra.Domain.Entities;

/// <summary>
/// Pending invitation to a tenant.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class Invite : TenantEntity
{
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Secure random token for invite link. Stored hashed in some systems,
    /// but for simplicity we store the token value directly (UNIQUE constraint).
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    public Guid InvitedByMemberId { get; private set; }
    public TenantMember InvitedByMember { get; private set; } = null!;

    /// <summary>
    /// Role IDs to assign when the invite is accepted.
    /// Stored as UUID[] in PostgreSQL.
    /// </summary>
    public List<Guid> RoleIds { get; private set; } = [];

    public string Status { get; private set; } = "pending";
    public DateTimeOffset ExpiresAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private Invite()
    {
        // EF Core constructor
    }

    internal static Invite Create(
        Guid tenantId,
        string email,
        Guid invitedByMemberId,
        DateTimeOffset expiresAt,
        string token,
        IEnumerable<Guid>? roleIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Expiration must be in the future.", nameof(expiresAt));

        var invite = new Invite
        {
            TenantId = tenantId,
            Email = email.ToLowerInvariant().Trim(),
            Token = token,
            InvitedByMemberId = invitedByMemberId,
            ExpiresAt = expiresAt,
            Status = "pending",
            RoleIds = roleIds?.ToList() ?? []
        };

        return invite;
    }

    public void Accept()
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot accept invite with status '{Status}'.");

        if (DateTimeOffset.UtcNow > ExpiresAt)
            throw new InvalidOperationException("Invite has expired.");

        Status = "accepted";
    }

    public void Cancel()
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot cancel invite with status '{Status}'.");

        Status = "cancelled";
    }

    public void MarkExpired()
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot expire invite with status '{Status}'.");

        Status = "expired";
    }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    public bool IsPending => Status == "pending" && !IsExpired;
}
