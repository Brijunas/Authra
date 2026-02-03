using Authra.Application.Common.DTOs;
using Authra.Application.Organizations.DTOs;

namespace Authra.Application.Organizations;

/// <summary>
/// Service for organization management within a tenant.
/// </summary>
public interface IOrganizationService
{
    // === Organization CRUD ===

    /// <summary>
    /// Creates a new organization within a tenant.
    /// </summary>
    Task<OrganizationResponse> CreateOrganizationAsync(Guid tenantId, CreateOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an organization by ID.
    /// </summary>
    Task<OrganizationResponse> GetOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists organizations within a tenant with cursor pagination.
    /// </summary>
    Task<PagedResponse<OrganizationResponse>> ListOrganizationsAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an organization's details.
    /// </summary>
    Task<OrganizationResponse> UpdateOrganizationAsync(Guid tenantId, Guid organizationId, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes (soft-delete) an organization.
    /// </summary>
    Task DeleteOrganizationAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default);

    // === Organization Members ===

    /// <summary>
    /// Adds a tenant member to an organization.
    /// </summary>
    Task<OrganizationMemberResponse> AddMemberAsync(Guid tenantId, Guid organizationId, Guid tenantMemberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists members of an organization with cursor pagination.
    /// </summary>
    Task<PagedResponse<OrganizationMemberResponse>> ListMembersAsync(Guid tenantId, Guid organizationId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from an organization.
    /// </summary>
    Task RemoveMemberAsync(Guid tenantId, Guid organizationId, Guid tenantMemberId, CancellationToken cancellationToken = default);
}
