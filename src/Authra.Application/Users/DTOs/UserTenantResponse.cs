namespace Authra.Application.Users.DTOs;

public record UserTenantResponse(
    string Id,
    string Name,
    string Slug,
    string MemberId,
    string Status,
    bool IsOwner,
    DateTimeOffset JoinedAt);
