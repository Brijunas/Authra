namespace Authra.Application.Organizations.DTOs;

// === Organization ===

public record CreateOrganizationRequest(
    string Name,
    string Slug);

public record UpdateOrganizationRequest(
    string? Name = null,
    string? Slug = null);

public record OrganizationResponse(
    string Id,
    string TenantId,
    string Name,
    string Slug,
    string Status,
    int MemberCount,
    DateTimeOffset CreatedAt);

// === Organization Members ===

public record AddOrganizationMemberRequest(
    string MemberId);

public record OrganizationMemberResponse(
    string Id,
    string TenantMemberId,
    string Email,
    string? Username,
    DateTimeOffset JoinedAt);
