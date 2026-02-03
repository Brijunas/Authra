namespace Authra.Domain.Entities;

/// <summary>
/// Tenant-defined role that groups permissions together.
/// Roles are assigned to TenantMembers via TenantMemberRole.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class Role : TenantEntity
{
    private readonly List<RolePermission> _rolePermissions = [];
    private readonly List<TenantMemberRole> _memberRoles = [];
    private readonly List<RoleOrganization> _organizationRestrictions = [];

    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Unique code within the tenant (e.g., "admin", "member", "viewer").
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable role name (e.g., "Administrator", "Member").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of the role's purpose.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// When TRUE, this role is automatically assigned to new tenant members.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// TRUE for system-defined roles that cannot be deleted (e.g., "owner" role).
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// v1.1 ready: When TRUE (and Tenant.AllowOrgRestrictions is TRUE),
    /// this role only applies to organizations listed in RoleOrganization.
    /// </summary>
    public bool IsRestricted { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();
    public IReadOnlyCollection<TenantMemberRole> MemberRoles => _memberRoles.AsReadOnly();
    public IReadOnlyCollection<RoleOrganization> OrganizationRestrictions => _organizationRestrictions.AsReadOnly();

    private Role()
    {
        // EF Core constructor
    }

    /// <summary>
    /// Creates a new role for a tenant.
    /// </summary>
    public static Role Create(Guid tenantId, string code, string name, string? description = null, bool isDefault = false, bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            TenantId = tenantId,
            Code = code.ToLowerInvariant().Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsDefault = isDefault,
            IsSystem = isSystem,
            IsRestricted = false
        };
    }

    /// <summary>
    /// Creates the special "owner" system role for a tenant.
    /// This role has all permissions and cannot be deleted.
    /// </summary>
    public static Role CreateOwnerRole(Guid tenantId)
    {
        return Create(tenantId, "owner", "Owner", "Full access to all tenant resources", isDefault: false, isSystem: true);
    }

    /// <summary>
    /// Creates a default "member" role for a tenant.
    /// Automatically assigned to new members if IsDefault is true.
    /// </summary>
    public static Role CreateDefaultMemberRole(Guid tenantId)
    {
        return Create(tenantId, "member", "Member", "Default member role", isDefault: true, isSystem: false);
    }

    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    /// <summary>
    /// Enables organization restriction for this role (v1.1).
    /// Only effective when Tenant.AllowOrgRestrictions is TRUE.
    /// </summary>
    public void EnableRestriction()
    {
        IsRestricted = true;
    }

    /// <summary>
    /// Disables organization restriction for this role.
    /// </summary>
    public void DisableRestriction()
    {
        IsRestricted = false;
    }

    /// <summary>
    /// Adds a permission to this role.
    /// </summary>
    public RolePermission AddPermission(Permission permission)
    {
        var rolePermission = RolePermission.Create(Id, permission.Id, TenantId);
        _rolePermissions.Add(rolePermission);
        return rolePermission;
    }

    /// <summary>
    /// Restricts this role to a specific organization (v1.1).
    /// </summary>
    public RoleOrganization RestrictToOrganization(Organization organization)
    {
        if (organization.TenantId != TenantId)
            throw new InvalidOperationException("Organization does not belong to this role's tenant.");

        var restriction = RoleOrganization.Create(Id, organization.Id, TenantId);
        _organizationRestrictions.Add(restriction);
        return restriction;
    }
}
