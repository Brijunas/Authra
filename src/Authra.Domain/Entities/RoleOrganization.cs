namespace Authra.Domain.Entities;

/// <summary>
/// Role restriction to specific organizations (v1.1 ready).
/// When Role.IsRestricted is TRUE and Tenant.AllowOrgRestrictions is TRUE,
/// the role only applies to organizations listed here.
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class RoleOrganization : TenantEntity
{
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    public Guid OrganizationId { get; private set; }
    public Organization Organization { get; private set; } = null!;

    public Tenant Tenant { get; private set; } = null!;

    private RoleOrganization()
    {
        // EF Core constructor
    }

    internal static RoleOrganization Create(Guid roleId, Guid organizationId, Guid tenantId)
    {
        return new RoleOrganization
        {
            RoleId = roleId,
            OrganizationId = organizationId,
            TenantId = tenantId
        };
    }
}
