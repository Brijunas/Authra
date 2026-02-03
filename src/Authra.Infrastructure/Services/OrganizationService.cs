using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Organizations;
using Authra.Application.Organizations.DTOs;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Organization service implementation for organization management within tenants.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _context;

    public OrganizationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationResponse> CreateOrganizationAsync(Guid tenantId, CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Check if slug is already taken within the tenant
        var normalizedSlug = request.Slug.ToLowerInvariant().Trim();
        var slugExists = await _context.Organizations
            .AnyAsync(o => o.TenantId == tenantId && o.Slug == normalizedSlug && o.Status != "deleted", cancellationToken);

        if (slugExists)
        {
            throw new ConflictException("Organization slug is already taken within this tenant");
        }

        var organization = tenant.AddOrganization(request.Name, request.Slug);
        await _context.SaveChangesAsync(cancellationToken);

        return await MapToResponseAsync(organization, cancellationToken);
    }

    public async Task<OrganizationResponse> GetOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.TenantId == tenantId && o.Status != "deleted", cancellationToken)
            ?? throw new NotFoundException("Organization", organizationId);

        return await MapToResponseAsync(organization, cancellationToken);
    }

    public async Task<PagedResponse<OrganizationResponse>> ListOrganizationsAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(pagination.Limit, 1, 100);

        var baseQuery = _context.Organizations
            .Where(o => o.TenantId == tenantId && o.Status != "deleted");

        // Apply cursor filter
        if (!string.IsNullOrEmpty(pagination.Cursor))
        {
            var cursorId = DecodeCursor(pagination.Cursor);
            baseQuery = baseQuery.Where(o => o.Id.CompareTo(cursorId) > 0);
        }

        var organizations = await baseQuery
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = organizations.Count > limit;
        var items = new List<OrganizationResponse>();

        foreach (var org in organizations.Take(limit))
        {
            items.Add(await MapToResponseAsync(org, cancellationToken));
        }

        var nextCursor = hasMore ? EncodeCursor(organizations[limit - 1].Id) : null;

        return new PagedResponse<OrganizationResponse>(items, nextCursor, hasMore);
    }

    public async Task<OrganizationResponse> UpdateOrganizationAsync(Guid tenantId, Guid organizationId, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.TenantId == tenantId && o.Status != "deleted", cancellationToken)
            ?? throw new NotFoundException("Organization", organizationId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            organization.UpdateName(request.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            // Check if new slug is taken within the tenant
            var normalizedSlug = request.Slug.ToLowerInvariant().Trim();
            var slugExists = await _context.Organizations
                .AnyAsync(o => o.TenantId == tenantId && o.Id != organizationId && o.Slug == normalizedSlug && o.Status != "deleted", cancellationToken);

            if (slugExists)
            {
                throw new ConflictException("Organization slug is already taken within this tenant");
            }

            organization.UpdateSlug(request.Slug);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await MapToResponseAsync(organization, cancellationToken);
    }

    public async Task DeleteOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.TenantId == tenantId && o.Status != "deleted", cancellationToken)
            ?? throw new NotFoundException("Organization", organizationId);

        organization.MarkDeleted();
        await _context.SaveChangesAsync(cancellationToken);
    }

    // === Organization Members ===

    public async Task<OrganizationMemberResponse> AddMemberAsync(Guid tenantId, Guid organizationId, Guid tenantMemberId, CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.TenantId == tenantId && o.Status != "deleted", cancellationToken)
            ?? throw new NotFoundException("Organization", organizationId);

        var tenantMember = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .FirstOrDefaultAsync(m => m.Id == tenantMemberId && m.TenantId == tenantId && m.Status == "active", cancellationToken)
            ?? throw new NotFoundException("TenantMember", tenantMemberId);

        // Check if already a member
        var existingMembership = await _context.OrganizationMembers
            .AnyAsync(om => om.OrganizationId == organizationId && om.TenantMemberId == tenantMemberId, cancellationToken);

        if (existingMembership)
        {
            throw new ConflictException("Member is already part of this organization");
        }

        var orgMember = organization.AddMember(tenantMember);
        _context.OrganizationMembers.Add(orgMember);
        await _context.SaveChangesAsync(cancellationToken);

        return MapMemberToResponse(orgMember, tenantMember);
    }

    public async Task<PagedResponse<OrganizationMemberResponse>> ListMembersAsync(Guid tenantId, Guid organizationId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        // Verify organization exists
        var organizationExists = await _context.Organizations
            .AnyAsync(o => o.Id == organizationId && o.TenantId == tenantId && o.Status != "deleted", cancellationToken);

        if (!organizationExists)
        {
            throw new NotFoundException("Organization", organizationId);
        }

        var limit = Math.Clamp(pagination.Limit, 1, 100);

        var baseQuery = _context.OrganizationMembers
            .Include(om => om.TenantMember)
                .ThenInclude(tm => tm.User)
                    .ThenInclude(u => u.Identifiers)
            .Where(om => om.OrganizationId == organizationId && om.TenantId == tenantId);

        // Apply cursor filter
        if (!string.IsNullOrEmpty(pagination.Cursor))
        {
            var cursorId = DecodeCursor(pagination.Cursor);
            baseQuery = baseQuery.Where(om => om.Id.CompareTo(cursorId) > 0);
        }

        var members = await baseQuery
            .OrderBy(om => om.JoinedAt)
            .ThenBy(om => om.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = members.Count > limit;
        var items = members.Take(limit).Select(om => MapMemberToResponse(om, om.TenantMember)).ToList();
        var nextCursor = hasMore ? EncodeCursor(members[limit - 1].Id) : null;

        return new PagedResponse<OrganizationMemberResponse>(items, nextCursor, hasMore);
    }

    public async Task RemoveMemberAsync(Guid tenantId, Guid organizationId, Guid tenantMemberId, CancellationToken cancellationToken = default)
    {
        var orgMember = await _context.OrganizationMembers
            .FirstOrDefaultAsync(om => om.OrganizationId == organizationId && om.TenantMemberId == tenantMemberId && om.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("OrganizationMember", tenantMemberId);

        _context.OrganizationMembers.Remove(orgMember);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // === Helper Methods ===

    private async Task<OrganizationResponse> MapToResponseAsync(Domain.Entities.Organization organization, CancellationToken cancellationToken)
    {
        var memberCount = await _context.OrganizationMembers
            .CountAsync(om => om.OrganizationId == organization.Id, cancellationToken);

        return new OrganizationResponse(
            IdPrefix.EncodeOrganization(organization.Id),
            IdPrefix.EncodeTenant(organization.TenantId),
            organization.Name,
            organization.Slug,
            organization.Status,
            memberCount,
            organization.CreatedAt);
    }

    private static OrganizationMemberResponse MapMemberToResponse(Domain.Entities.OrganizationMember orgMember, Domain.Entities.TenantMember tenantMember)
    {
        var email = tenantMember.User.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized ?? "";
        var username = tenantMember.User.Identifiers.FirstOrDefault(i => i.Type == "username")?.ValueNormalized;

        return new OrganizationMemberResponse(
            IdPrefix.EncodeMember(orgMember.Id),
            IdPrefix.EncodeMember(orgMember.TenantMemberId),
            email,
            username,
            orgMember.JoinedAt);
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
