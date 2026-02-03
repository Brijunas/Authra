namespace Authra.Application.Auth.DTOs;

// === Registration ===

public record RegisterRequest(
    string Email,
    string Password,
    string? Username = null);

public record RegisterResponse(
    string UserId,
    string Email,
    string? Username);

// === Login ===

public record LoginRequest(
    string Email,
    string Password,
    string? TenantId = null);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserInfo User,
    TenantInfo? Tenant);

public record UserInfo(
    string Id,
    string Email,
    string? Username);

public record TenantInfo(
    string Id,
    string Name,
    string Slug,
    string MemberId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

// === Tenant Selection (for users with multiple tenants) ===

public record TenantSelectionRequired(
    string UserId,
    IReadOnlyList<AvailableTenant> AvailableTenants);

public record AvailableTenant(
    string Id,
    string Name,
    string Slug);

// === Token Refresh ===

public record RefreshRequest(
    string RefreshToken);

public record RefreshResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

// === Tenant Switch ===

public record SwitchTenantRequest(
    string TenantId);

// === Password Reset ===

public record PasswordResetRequestDto(
    string Email);

public record PasswordResetDto(
    string Token,
    string NewPassword);

// === Logout ===

public record LogoutRequest(
    string? RefreshToken = null,
    bool LogoutAll = false);
