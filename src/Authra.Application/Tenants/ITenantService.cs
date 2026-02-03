using Authra.Application.Common.DTOs;
using Authra.Application.Tenants.DTOs;

namespace Authra.Application.Tenants;

/// <summary>
/// Service for tenant management operations.
/// </summary>
public interface ITenantService
{
    // === Tenant CRUD ===

    /// <summary>
    /// Creates a new tenant with the current user as owner.
    /// </summary>
    Task<TenantResponse> CreateTenantAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    Task<TenantResponse> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a tenant's details.
    /// </summary>
    Task<TenantResponse> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers tenant ownership to another member.
    /// </summary>
    Task TransferOwnershipAsync(Guid tenantId, Guid currentOwnerMemberId, Guid newOwnerMemberId, CancellationToken cancellationToken = default);

    // === Members ===

    /// <summary>
    /// Lists tenant members with cursor pagination.
    /// </summary>
    Task<PagedResponse<TenantMemberResponse>> ListMembersAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tenant member.
    /// </summary>
    Task<TenantMemberResponse> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a tenant member.
    /// </summary>
    Task<TenantMemberResponse> SuspendMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a suspended tenant member.
    /// </summary>
    Task<TenantMemberResponse> ActivateMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from the tenant.
    /// </summary>
    Task RemoveMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);
}
