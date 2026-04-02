namespace Authra.Application.Auth.DTOs;

public record PasswordResetDto(
    string Token,
    string NewPassword);
