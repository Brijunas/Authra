namespace Authra.Application.Users.DTOs;

// === Current User ===

public record CurrentUserResponse(
    string Id,
    string Email,
    string? Username,
    DateTimeOffset CreatedAt);

public record UpdateUsernameRequest(
    string Username);

// === User's Tenants ===

public record UserTenantResponse(
    string Id,
    string Name,
    string Slug,
    string MemberId,
    string Status,
    bool IsOwner,
    DateTimeOffset JoinedAt);
