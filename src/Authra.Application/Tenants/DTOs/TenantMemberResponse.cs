namespace Authra.Application.Tenants.DTOs;

public record TenantMemberResponse(
    string Id,
    string UserId,
    string Email,
    string? Username,
    string Status,
    bool IsOwner,
    IReadOnlyList<string> Roles,
    DateTimeOffset JoinedAt);
