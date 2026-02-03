using Authra.Domain.Entities;

namespace Authra.Infrastructure.Persistence.Seeding;

/// <summary>
/// Provides the 19 MVP system permissions for seeding.
/// These permissions are global (TenantId = NULL) and cannot be modified or deleted.
/// </summary>
public static class SystemPermissionSeeder
{
    /// <summary>
    /// Gets the list of MVP system permissions to seed.
    /// </summary>
    public static IReadOnlyList<Permission> GetSystemPermissions()
    {
        return
        [
            // Tenant permissions
            Permission.CreateSystemPermission("tenant:read", "View Tenant Settings", "Allows viewing tenant settings and configuration", "Tenant"),
            Permission.CreateSystemPermission("tenant:update", "Update Tenant Settings", "Allows modifying tenant settings and configuration", "Tenant"),
            Permission.CreateSystemPermission("tenant:transfer", "Transfer Ownership", "Allows transferring tenant ownership to another member", "Tenant"),

            // Account permissions
            Permission.CreateSystemPermission("accounts:read", "View Accounts", "Allows viewing tenant member accounts", "Accounts"),
            Permission.CreateSystemPermission("accounts:invite", "Invite Accounts", "Allows inviting new members to the tenant", "Accounts"),
            Permission.CreateSystemPermission("accounts:update", "Update Accounts", "Allows modifying tenant member accounts", "Accounts"),
            Permission.CreateSystemPermission("accounts:suspend", "Suspend Accounts", "Allows suspending tenant member accounts", "Accounts"),
            Permission.CreateSystemPermission("accounts:remove", "Remove Accounts", "Allows removing members from the tenant", "Accounts"),

            // Organization permissions
            Permission.CreateSystemPermission("organizations:read", "View Organizations", "Allows viewing organizations within the tenant", "Organizations"),
            Permission.CreateSystemPermission("organizations:create", "Create Organizations", "Allows creating new organizations within the tenant", "Organizations"),
            Permission.CreateSystemPermission("organizations:update", "Update Organizations", "Allows modifying organization settings", "Organizations"),
            Permission.CreateSystemPermission("organizations:delete", "Delete Organizations", "Allows deleting organizations from the tenant", "Organizations"),
            Permission.CreateSystemPermission("organizations:members.read", "View Org Members", "Allows viewing organization member lists", "Organizations"),
            Permission.CreateSystemPermission("organizations:members.write", "Manage Org Members", "Allows adding and removing organization members", "Organizations"),

            // Role permissions
            Permission.CreateSystemPermission("roles:read", "View Roles", "Allows viewing roles and their permissions", "Roles"),
            Permission.CreateSystemPermission("roles:create", "Create Roles", "Allows creating new roles within the tenant", "Roles"),
            Permission.CreateSystemPermission("roles:update", "Update Roles", "Allows modifying role settings and permissions", "Roles"),
            Permission.CreateSystemPermission("roles:delete", "Delete Roles", "Allows deleting roles from the tenant", "Roles"),
            Permission.CreateSystemPermission("roles:assign", "Assign Roles", "Allows assigning and unassigning roles to members", "Roles"),
        ];
    }

    /// <summary>
    /// Gets the permission codes for the Owner role (all permissions).
    /// </summary>
    public static IReadOnlyList<string> GetOwnerRolePermissionCodes()
    {
        return GetSystemPermissions().Select(p => p.Code).ToList();
    }

    /// <summary>
    /// Gets the permission codes for a default Member role.
    /// Basic read-only access to tenant resources.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultMemberPermissionCodes()
    {
        return
        [
            "tenant:read",
            "accounts:read",
            "organizations:read",
            "organizations:members.read",
            "roles:read"
        ];
    }
}
