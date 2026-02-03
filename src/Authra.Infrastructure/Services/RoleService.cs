using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Roles;
using Authra.Application.Roles.DTOs;
using Authra.Domain.Entities;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Role service implementation for role and permission management within tenants.
/// </summary>
public class RoleService : IRoleService
{
    private readonly AppDbContext _context;

    public RoleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RoleResponse> CreateRoleAsync(Guid tenantId, CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        // Verify tenant exists
        var tenantExists = await _context.Tenants
            .AnyAsync(t => t.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            throw new NotFoundException("Tenant", tenantId);
        }

        // Check if code is already taken within the tenant
        var normalizedCode = request.Code.ToLowerInvariant().Trim();
        var codeExists = await _context.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            throw new ConflictException("Role code is already taken within this tenant");
        }

        var role = Role.Create(tenantId, request.Code, request.Name, request.Description, request.IsDefault);
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        // Add permissions if specified
        if (request.PermissionIds?.Count > 0)
        {
            var permissionGuids = request.PermissionIds.Select(IdPrefix.DecodePermission).ToList();
            var permissions = await _context.Permissions
                .Where(p => permissionGuids.Contains(p.Id) && (p.TenantId == null || p.TenantId == tenantId))
                .ToListAsync(cancellationToken);

            foreach (var permission in permissions)
            {
                var rolePermission = role.AddPermission(permission);
                _context.RolePermissions.Add(rolePermission);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await GetRoleResponseAsync(role.Id, cancellationToken);
    }

    public async Task<RoleResponse> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Role", roleId);

        return MapToResponse(role);
    }

    public async Task<PagedResponse<RoleResponse>> ListRolesAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(pagination.Limit, 1, 100);

        var baseQuery = _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.TenantId == tenantId);

        // Apply cursor filter
        if (!string.IsNullOrEmpty(pagination.Cursor))
        {
            var cursorId = DecodeCursor(pagination.Cursor);
            baseQuery = baseQuery.Where(r => r.Id.CompareTo(cursorId) > 0);
        }

        var roles = await baseQuery
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = roles.Count > limit;
        var items = roles.Take(limit).Select(MapToResponse).ToList();
        var nextCursor = hasMore ? EncodeCursor(roles[limit - 1].Id) : null;

        return new PagedResponse<RoleResponse>(items, nextCursor, hasMore);
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid tenantId, Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Role", roleId);

        // Cannot modify system roles (except permissions on owner role)
        if (role.IsSystem && (request.Name != null || request.Description != null || request.IsDefault != null))
        {
            throw new ForbiddenException("Cannot modify system role properties");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            role.UpdateName(request.Name);
        }

        if (request.Description != null)
        {
            role.UpdateDescription(request.Description);
        }

        if (request.IsDefault.HasValue && !role.IsSystem)
        {
            role.SetDefault(request.IsDefault.Value);
        }

        // Update permissions if specified
        if (request.PermissionIds != null)
        {
            // Remove existing permissions
            var existingPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(cancellationToken);
            _context.RolePermissions.RemoveRange(existingPermissions);

            // Add new permissions
            if (request.PermissionIds.Count > 0)
            {
                var permissionGuids = request.PermissionIds.Select(IdPrefix.DecodePermission).ToList();
                var permissions = await _context.Permissions
                    .Where(p => permissionGuids.Contains(p.Id) && (p.TenantId == null || p.TenantId == tenantId))
                    .ToListAsync(cancellationToken);

                foreach (var permission in permissions)
                {
                    var rolePermission = role.AddPermission(permission);
                    _context.RolePermissions.Add(rolePermission);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRoleResponseAsync(roleId, cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Role", roleId);

        if (role.IsSystem)
        {
            throw new ForbiddenException("Cannot delete system roles");
        }

        // Check if any members have this role
        var hasAssignments = await _context.TenantMemberRoles
            .AnyAsync(tmr => tmr.RoleId == roleId, cancellationToken);

        if (hasAssignments)
        {
            throw new ConflictException("Cannot delete role that is assigned to members. Remove assignments first.");
        }

        // Remove role permissions
        var rolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);
        _context.RolePermissions.RemoveRange(rolePermissions);

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // === Role Assignment ===

    public async Task<IReadOnlyList<MemberRoleResponse>> ListMemberRolesAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _context.TenantMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        var assignments = await _context.TenantMemberRoles
            .Include(tmr => tmr.Role)
            .Where(tmr => tmr.TenantMemberId == memberId && tmr.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return assignments.Select(MapAssignmentToResponse).ToList();
    }

    public async Task<MemberRoleResponse> AssignRoleAsync(Guid tenantId, Guid memberId, Guid roleId, Guid? assignedBy = null, CancellationToken cancellationToken = default)
    {
        var member = await _context.TenantMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Role", roleId);

        // Check if already assigned
        var existingAssignment = await _context.TenantMemberRoles
            .AnyAsync(tmr => tmr.TenantMemberId == memberId && tmr.RoleId == roleId, cancellationToken);

        if (existingAssignment)
        {
            throw new ConflictException("Role is already assigned to this member");
        }

        var assignment = TenantMemberRole.Create(memberId, roleId, tenantId, assignedBy);
        _context.TenantMemberRoles.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with role for response
        var savedAssignment = await _context.TenantMemberRoles
            .Include(tmr => tmr.Role)
            .FirstAsync(tmr => tmr.Id == assignment.Id, cancellationToken);

        return MapAssignmentToResponse(savedAssignment);
    }

    public async Task UnassignRoleAsync(Guid tenantId, Guid memberId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var assignment = await _context.TenantMemberRoles
            .Include(tmr => tmr.Role)
            .FirstOrDefaultAsync(tmr => tmr.TenantMemberId == memberId && tmr.RoleId == roleId && tmr.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("RoleAssignment", roleId);

        // Cannot unassign owner role from owner
        if (assignment.Role.Code == "owner")
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

            if (tenant?.OwnerMemberId == memberId)
            {
                throw new ForbiddenException("Cannot unassign owner role from the tenant owner");
            }
        }

        _context.TenantMemberRoles.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // === Permissions ===

    public async Task<IReadOnlyList<PermissionResponse>> ListSystemPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Permissions
            .Where(p => p.TenantId == null && p.IsSystem)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return permissions.Select(MapPermissionToResponse).ToList();
    }

    // === Helper Methods ===

    private async Task<RoleResponse> GetRoleResponseAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == roleId, cancellationToken);

        return MapToResponse(role);
    }

    private static RoleResponse MapToResponse(Role role)
    {
        var permissions = role.RolePermissions
            .Select(rp => MapPermissionToResponse(rp.Permission))
            .ToList();

        return new RoleResponse(
            IdPrefix.EncodeRole(role.Id),
            IdPrefix.EncodeTenant(role.TenantId),
            role.Code,
            role.Name,
            role.Description,
            role.IsDefault,
            role.IsSystem,
            permissions,
            role.CreatedAt);
    }

    private static PermissionResponse MapPermissionToResponse(Permission permission)
    {
        return new PermissionResponse(
            IdPrefix.EncodePermission(permission.Id),
            permission.Code,
            permission.Name,
            permission.Description,
            permission.Category,
            permission.IsSystem);
    }

    private static MemberRoleResponse MapAssignmentToResponse(TenantMemberRole assignment)
    {
        return new MemberRoleResponse(
            IdPrefix.EncodeRole(assignment.RoleId),
            assignment.Role.Code,
            assignment.Role.Name,
            assignment.AssignedAt,
            assignment.AssignedBy.HasValue ? IdPrefix.EncodeMember(assignment.AssignedBy.Value) : null);
    }

    private static string EncodeCursor(Guid id) => Convert.ToBase64String(id.ToByteArray());

    private static Guid DecodeCursor(string cursor)
    {
        try
        {
            return new Guid(Convert.FromBase64String(cursor));
        }
        catch
        {
            throw new ValidationException("Invalid cursor format");
        }
    }
}
