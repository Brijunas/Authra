namespace Authra.Application.Organizations.DTOs;

public record OrganizationMemberResponse(
    string Id,
    string TenantMemberId,
    string Email,
    string? Username,
    DateTimeOffset JoinedAt);
