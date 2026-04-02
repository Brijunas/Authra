namespace Authra.Application.Auth.DTOs;

public record UserAuth(
    string Id,
    string Email,
    string? Username);
