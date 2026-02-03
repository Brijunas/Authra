using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Tenants;
using Authra.Application.Tenants.DTOs;
using Authra.Domain.Entities;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Authra.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Tenant service implementation for tenant management.
/// </summary>
public class TenantService : ITenantService
{
    private readonly AppDbContext _context;

    public TenantService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TenantResponse> CreateTenantAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Check if slug is already taken
        var slugExists = await _context.Tenants
            .AnyAsync(t => t.Slug == request.Slug.ToLowerInvariant().Trim(), cancellationToken);

        if (slugExists)
        {
            throw new ConflictException("Tenant slug is already taken");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Create tenant
        var tenant = Tenant.Create(request.Name, request.Slug);
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        // Add creator as member
        var member = tenant.AddMember(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Set owner
        tenant.SetOwner(member);
        await _context.SaveChangesAsync(cancellationToken);

        // Create owner role with all permissions
        var ownerRole = Role.CreateOwnerRole(tenant.Id);
        _context.Roles.Add(ownerRole);
        await _context.SaveChangesAsync(cancellationToken);

        // Add all system permissions to owner role
        var systemPermissions = await _context.Permissions
            .Where(p => p.TenantId == null && p.IsSystem)
            .ToListAsync(cancellationToken);

        foreach (var permission in systemPermissions)
        {
            var rolePermission = ownerRole.AddPermission(permission);
            _context.RolePermissions.Add(rolePermission);
        }
        await _context.SaveChangesAsync(cancellationToken);

        // Assign owner role to the member
        var ownerRoleAssignment = member.AssignRole(ownerRole);
        _context.TenantMemberRoles.Add(ownerRoleAssignment);
        await _context.SaveChangesAsync(cancellationToken);

        // Create default member role
        var memberRole = Role.CreateDefaultMemberRole(tenant.Id);
        _context.Roles.Add(memberRole);

        // Add default permissions to member role
        var defaultPermissionCodes = SystemPermissionSeeder.GetDefaultMemberPermissionCodes();
        var defaultPermissions = systemPermissions
            .Where(p => defaultPermissionCodes.Contains(p.Code))
            .ToList();

        foreach (var permission in defaultPermissions)
        {
            var rolePermission = memberRole.AddPermission(permission);
            _context.RolePermissions.Add(rolePermission);
        }
        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            tenant.UpdateName(request.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            // Check if new slug is taken
            var normalizedSlug = request.Slug.ToLowerInvariant().Trim();
            var slugExists = await _context.Tenants
                .AnyAsync(t => t.Id != tenantId && t.Slug == normalizedSlug, cancellationToken);

            if (slugExists)
            {
                throw new ConflictException("Tenant slug is already taken");
            }

            tenant.UpdateSlug(request.Slug);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task TransferOwnershipAsync(Guid tenantId, Guid currentOwnerMemberId, Guid newOwnerMemberId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Verify current owner
        if (tenant.OwnerMemberId != currentOwnerMemberId)
        {
            throw new ForbiddenException("Only the current owner can transfer ownership");
        }

        // Get new owner member
        var newOwner = await _context.TenantMembers
            .Include(m => m.RoleAssignments)
            .FirstOrDefaultAsync(m => m.Id == newOwnerMemberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", newOwnerMemberId);

        if (!newOwner.IsActive)
        {
            throw new ValidationException("Cannot transfer ownership to a suspended or removed member");
        }

        // Get owner role
        var ownerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == "owner", cancellationToken)
            ?? throw new NotFoundException("Owner role not found");

        // Remove owner role from current owner
        var currentOwner = await _context.TenantMembers
            .Include(m => m.RoleAssignments)
            .FirstOrDefaultAsync(m => m.Id == currentOwnerMemberId, cancellationToken);

        if (currentOwner != null)
        {
            var currentOwnerRoleAssignment = currentOwner.RoleAssignments
                .FirstOrDefault(ra => ra.RoleId == ownerRole.Id);

            if (currentOwnerRoleAssignment != null)
            {
                _context.TenantMemberRoles.Remove(currentOwnerRoleAssignment);
            }
        }

        // Assign owner role to new owner if not already assigned
        if (!newOwner.RoleAssignments.Any(ra => ra.RoleId == ownerRole.Id))
        {
            newOwner.AssignRole(ownerRole, currentOwnerMemberId);
        }

        // Transfer ownership
        tenant.SetOwner(newOwner);

        // Record ownership transfer for audit (MVP: immediate transfer)
        var transfer = OwnershipTransfer.CreateImmediate(
            tenantId,
            currentOwnerMemberId,
            newOwnerMemberId,
            currentOwnerMemberId);
        _context.OwnershipTransfers.Add(transfer);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<TenantMemberResponse>> ListMembersAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(pagination.Limit, 1, 100);

        var baseQuery = _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Include(m => m.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .Include(m => m.Tenant)
            .Where(m => m.TenantId == tenantId && m.Status != "removed");

        // Apply cursor filter
        if (!string.IsNullOrEmpty(pagination.Cursor))
        {
            var cursorId = DecodeCursor(pagination.Cursor);
            baseQuery = baseQuery.Where(m => m.Id.CompareTo(cursorId) > 0);
        }

        var members = await baseQuery
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = members.Count > limit;
        var items = members.Take(limit).Select(m => MapMemberToResponse(m)).ToList();
        var nextCursor = hasMore ? EncodeCursor(members[limit - 1].Id) : null;

        return new PagedResponse<TenantMemberResponse>(items, nextCursor, hasMore);
    }

    public async Task<TenantMemberResponse> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Include(m => m.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        return MapMemberToResponse(member);
    }

    public async Task<TenantMemberResponse> SuspendMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Cannot suspend the owner
        if (tenant.OwnerMemberId == memberId)
        {
            throw new ForbiddenException("Cannot suspend the tenant owner");
        }

        var member = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Include(m => m.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        member.Suspend();
        await _context.SaveChangesAsync(cancellationToken);

        return MapMemberToResponse(member);
    }

    public async Task<TenantMemberResponse> ActivateMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Include(m => m.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        member.Activate();
        await _context.SaveChangesAsync(cancellationToken);

        return MapMemberToResponse(member);
    }

    public async Task RemoveMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Cannot remove the owner
        if (tenant.OwnerMemberId == memberId)
        {
            throw new ForbiddenException("Cannot remove the tenant owner. Transfer ownership first.");
        }

        var member = await _context.TenantMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Member", memberId);

        member.Remove();
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static TenantResponse MapToResponse(Tenant tenant)
    {
        return new TenantResponse(
            IdPrefix.EncodeTenant(tenant.Id),
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.OwnerMemberId.HasValue ? IdPrefix.EncodeMember(tenant.OwnerMemberId.Value) : null,
            tenant.CreatedAt);
    }

    private static TenantMemberResponse MapMemberToResponse(TenantMember member)
    {
        var email = member.User.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized ?? "";
        var username = member.User.Identifiers.FirstOrDefault(i => i.Type == "username")?.ValueNormalized;

        var roles = member.RoleAssignments.Select(ra => ra.Role.Code).ToList();

        return new TenantMemberResponse(
            IdPrefix.EncodeMember(member.Id),
            IdPrefix.EncodeUser(member.UserId),
            email,
            username,
            member.Status,
            member.Tenant.OwnerMemberId == member.Id,
            roles,
            member.JoinedAt);
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
