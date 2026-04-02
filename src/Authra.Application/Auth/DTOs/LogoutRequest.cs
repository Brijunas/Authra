namespace Authra.Application.Auth.DTOs;

public record LogoutRequest(
    string? RefreshToken = null,
    bool LogoutAll = false);
