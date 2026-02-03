namespace Authra.Domain.Entities;

/// <summary>
/// Member assignment to an organization.
/// Links a TenantMember to an Organization within the same tenant.
/// Tenant-scoped entity with RLS support (TenantId denormalized for RLS).
/// </summary>
public class OrganizationMember : TenantEntity
{
    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = null!;

    public Guid TenantMemberId { get; private set; }
    public TenantMember TenantMember { get; private set; } = null!;

    public DateTimeOffset JoinedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private OrganizationMember()
    {
        // EF Core constructor
    }

    internal static OrganizationMember Create(Guid organizationId, Guid tenantMemberId, Guid tenantId)
    {
        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            TenantMemberId = tenantMemberId,
            TenantId = tenantId,
            JoinedAt = DateTimeOffset.UtcNow
        };

        return member;
    }
}
