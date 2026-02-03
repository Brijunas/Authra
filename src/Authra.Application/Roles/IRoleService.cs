using Authra.Application.Common.DTOs;
using Authra.Application.Roles.DTOs;

namespace Authra.Application.Roles;

/// <summary>
/// Service for role and permission management within a tenant.
/// </summary>
public interface IRoleService
{
    // === Role CRUD ===

    /// <summary>
    /// Creates a new role within a tenant.
    /// </summary>
    Task<RoleResponse> CreateRoleAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a role by ID.
    /// </summary>
    Task<RoleResponse> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists roles within a tenant with cursor pagination.
    /// </summary>
    Task<PagedResponse<RoleResponse>> ListRolesAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a role's details and permissions.
    /// </summary>
    Task<RoleResponse> UpdateRoleAsync(Guid tenantId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role (cannot delete system roles).
    /// </summary>
    Task DeleteRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    // === Role Assignment ===

    /// <summary>
    /// Lists roles assigned to a member.
    /// </summary>
    Task<IReadOnlyList<MemberRoleResponse>> ListMemberRolesAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a member.
    /// </summary>
    Task<MemberRoleResponse> AssignRoleAsync(Guid tenantId, Guid memberId, Guid roleId, Guid? assignedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unassigns a role from a member.
    /// </summary>
    Task UnassignRoleAsync(Guid tenantId, Guid memberId, Guid roleId, CancellationToken cancellationToken = default);

    // === Permissions ===

    /// <summary>
    /// Lists all system permissions (global, not tenant-specific).
    /// </summary>
    Task<IReadOnlyList<PermissionResponse>> ListSystemPermissionsAsync(CancellationToken cancellationToken = default);
}
