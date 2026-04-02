namespace Authra.Application.Auth.DTOs;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserAuth User,
    TenantInfo? Tenant);
