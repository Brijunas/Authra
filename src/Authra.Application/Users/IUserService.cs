using Authra.Application.Users.DTOs;

namespace Authra.Application.Users;

/// <summary>
/// Service for current user operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets the current user's profile.
    /// </summary>
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current user's username.
    /// </summary>
    Task<CurrentUserResponse> UpdateUsernameAsync(Guid userId, string newUsername, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of tenants the user belongs to.
    /// </summary>
    Task<IReadOnlyList<UserTenantResponse>> GetUserTenantsAsync(Guid userId, CancellationToken cancellationToken = default);
}
