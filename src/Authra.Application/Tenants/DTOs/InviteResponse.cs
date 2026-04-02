namespace Authra.Application.Tenants.DTOs;

public record InviteResponse(
    string Id,
    string Email,
    string Status,
    string InvitedById,
    string? InvitedByEmail,
    IReadOnlyList<string>? RoleIds,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
