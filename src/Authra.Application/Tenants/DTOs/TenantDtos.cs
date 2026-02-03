namespace Authra.Application.Tenants.DTOs;

// === Tenant ===

public record CreateTenantRequest(
    string Name,
    string Slug);

public record UpdateTenantRequest(
    string? Name = null,
    string? Slug = null);

public record TenantResponse(
    string Id,
    string Name,
    string Slug,
    string Status,
    string? OwnerId,
    DateTimeOffset CreatedAt);

public record TransferOwnershipRequest(
    string NewOwnerMemberId);

// === Tenant Members ===

public record TenantMemberResponse(
    string Id,
    string UserId,
    string Email,
    string? Username,
    string Status,
    bool IsOwner,
    IReadOnlyList<string> Roles,
    DateTimeOffset JoinedAt);

public record UpdateMemberRequest(
    string? Status = null);

// === Invites ===

public record CreateInviteRequest(
    string Email,
    IReadOnlyList<string>? RoleIds = null);

public record InviteResponse(
    string Id,
    string Email,
    string Status,
    string InvitedById,
    string? InvitedByEmail,
    IReadOnlyList<string>? RoleIds,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public record AcceptInviteRequest(
    string Token);
