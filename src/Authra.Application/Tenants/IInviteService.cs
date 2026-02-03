using Authra.Application.Common.DTOs;
using Authra.Application.Tenants.DTOs;

namespace Authra.Application.Tenants;

/// <summary>
/// Service for tenant invitation management.
/// </summary>
public interface IInviteService
{
    /// <summary>
    /// Creates an invitation for a user to join the tenant.
    /// </summary>
    Task<InviteResponse> CreateInviteAsync(Guid tenantId, Guid invitedByMemberId, CreateInviteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists pending invitations for a tenant.
    /// </summary>
    Task<PagedResponse<InviteResponse>> ListInvitesAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending invitation.
    /// </summary>
    Task CancelInviteAsync(Guid tenantId, Guid inviteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an invitation to join a tenant.
    /// </summary>
    Task<TenantMemberResponse> AcceptInviteAsync(Guid tenantId, string token, Guid userId, CancellationToken cancellationToken = default);
}
