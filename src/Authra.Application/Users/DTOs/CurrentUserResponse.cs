namespace Authra.Application.Users.DTOs;

public record CurrentUserResponse(
    string Id,
    string Email,
    string? Username,
    DateTimeOffset CreatedAt);
