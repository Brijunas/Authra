namespace Authra.Domain.Entities;

/// <summary>
/// Permission assignment to a role.
/// Junction table linking Role to Permission.
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class RolePermission : TenantEntity
{
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;

    public Guid PermissionId { get; private set; }
    public Permission Permission { get; private set; } = null!;

    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Timestamp when this permission was granted to the role.
    /// </summary>
    public DateTimeOffset GrantedAt { get; private set; }

    private RolePermission()
    {
        // EF Core constructor
    }

    internal static RolePermission Create(Guid roleId, Guid permissionId, Guid tenantId)
    {
        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            TenantId = tenantId,
            GrantedAt = DateTimeOffset.UtcNow
        };
    }
}
