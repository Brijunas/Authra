namespace Authra.Domain.Entities;

/// <summary>
/// System and tenant-defined permissions.
/// Global entity with optional TenantId (NULL = system permission).
/// System permissions have IsSystem = true and TenantId = NULL.
/// </summary>
public class Permission : Entity
{
    private readonly List<RolePermission> _rolePermissions = [];

    /// <summary>
    /// TenantId is NULL for system permissions.
    /// For tenant-defined custom permissions (v1.1), this references the owning tenant.
    /// </summary>
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }

    /// <summary>
    /// Permission code using colon notation (e.g., "tenant:read", "organizations:members.write").
    /// Must be unique within the tenant scope (or system scope if TenantId is NULL).
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable permission name (e.g., "View Tenant Settings").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of what this permission grants.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Category for grouping permissions in UI (e.g., "Tenant", "Organizations", "Roles").
    /// </summary>
    public string? Category { get; private set; }

    /// <summary>
    /// TRUE for system-defined permissions that cannot be modified or deleted.
    /// </summary>
    public bool IsSystem { get; private set; } = true;

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Permission()
    {
        // EF Core constructor
    }

    /// <summary>
    /// Creates a new system permission (TenantId = NULL, IsSystem = TRUE).
    /// Used for seeding the 19 MVP system permissions.
    /// </summary>
    public static Permission CreateSystemPermission(string code, string name, string? description = null, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Permission
        {
            TenantId = null,
            Code = code.ToLowerInvariant().Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            IsSystem = true
        };
    }

    /// <summary>
    /// Creates a tenant-defined custom permission (v1.1).
    /// Requires Tenant.AllowCustomPermissions to be TRUE.
    /// </summary>
    public static Permission CreateTenantPermission(Guid tenantId, string code, string name, string? description = null, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Permission
        {
            TenantId = tenantId,
            Code = code.ToLowerInvariant().Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            IsSystem = false
        };
    }

    public void UpdateDescription(string? description)
    {
        if (!IsSystem)
        {
            Description = description?.Trim();
        }
    }

    public void UpdateCategory(string? category)
    {
        if (!IsSystem)
        {
            Category = category?.Trim();
        }
    }
}
