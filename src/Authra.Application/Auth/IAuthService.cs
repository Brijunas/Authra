using Authra.Application.Auth.DTOs;
using OneOf;

namespace Authra.Application.Auth;

/// <summary>
/// Authentication service for user registration, login, and password management.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user with email and password.
    /// </summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user. Returns LoginResponse for single tenant, or TenantSelectionRequired for multi-tenant.
    /// </summary>
    Task<OneOf<LoginResponse, TenantSelectionRequired>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes login after tenant selection (for multi-tenant users).
    /// </summary>
    Task<LoginResponse> CompleteLoginAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes tokens using a refresh token.
    /// </summary>
    Task<RefreshResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the current user to a different tenant.
    /// </summary>
    Task<LoginResponse> SwitchTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a password reset email.
    /// </summary>
    Task RequestPasswordResetAsync(PasswordResetRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets password using a reset token.
    /// </summary>
    Task ResetPasswordAsync(PasswordResetDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out the user, revoking the current or all sessions.
    /// </summary>
    Task LogoutAsync(Guid userId, Guid tenantId, string? refreshToken, string? accessToken, bool logoutAll, CancellationToken cancellationToken = default);
}
