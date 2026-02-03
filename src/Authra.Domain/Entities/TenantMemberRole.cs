namespace Authra.Domain.Entities;

/// <summary>
/// Role assignment to a tenant member.
/// Junction table linking TenantMember to Role.
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class TenantMemberRole : TenantEntity
{
    public Guid TenantMemberId { get; private set; }
    public TenantMember TenantMember { get; private set; } = null!;

    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Timestamp when this role was assigned to the member.
    /// </summary>
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>
    /// The TenantMember who assigned this role. NULL if assigned by system or assigner was removed.
    /// </summary>
    public Guid? AssignedBy { get; private set; }
    public TenantMember? AssignedByMember { get; private set; }

    private TenantMemberRole()
    {
        // EF Core constructor
    }

    /// <summary>
    /// Creates a new role assignment for a tenant member.
    /// </summary>
    public static TenantMemberRole Create(Guid tenantMemberId, Guid roleId, Guid tenantId, Guid? assignedBy = null)
    {
        return new TenantMemberRole
        {
            TenantMemberId = tenantMemberId,
            RoleId = roleId,
            TenantId = tenantId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedBy = assignedBy
        };
    }
}
