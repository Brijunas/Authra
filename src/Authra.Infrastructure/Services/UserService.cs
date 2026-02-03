using Authra.Application.Common;
using Authra.Application.Users;
using Authra.Application.Users.DTOs;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Services;

/// <summary>
/// User service implementation for current user operations.
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.Identifiers)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var email = user.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized ?? "";
        var username = user.Identifiers.FirstOrDefault(i => i.Type == "username")?.ValueNormalized;

        return new CurrentUserResponse(
            IdPrefix.EncodeUser(user.Id),
            email,
            username,
            user.CreatedAt);
    }

    public async Task<CurrentUserResponse> UpdateUsernameAsync(Guid userId, string newUsername, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = newUsername.ToLowerInvariant().Trim();

        // Check if username is already taken
        var existingUsername = await _context.UserIdentifiers
            .FirstOrDefaultAsync(ui => ui.Type == "username" && ui.ValueNormalized == normalizedUsername, cancellationToken);

        if (existingUsername != null && existingUsername.UserId != userId)
        {
            throw new ConflictException("Username is already taken");
        }

        var user = await _context.Users
            .Include(u => u.Identifiers)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Find existing username identifier
        var usernameIdentifier = user.Identifiers.FirstOrDefault(i => i.Type == "username");

        if (usernameIdentifier != null)
        {
            // Update existing - we need to remove and re-add since UserIdentifier is immutable
            _context.UserIdentifiers.Remove(usernameIdentifier);
        }

        // Add new username identifier
        user.AddIdentifier("username", newUsername);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCurrentUserAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserTenantResponse>> GetUserTenantsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var memberships = await _context.TenantMembers
            .Include(tm => tm.Tenant)
            .Where(tm => tm.UserId == userId && tm.Tenant.Status != "deleted")
            .OrderBy(tm => tm.Tenant.Name)
            .ToListAsync(cancellationToken);

        return memberships.Select(m => new UserTenantResponse(
            IdPrefix.EncodeTenant(m.TenantId),
            m.Tenant.Name,
            m.Tenant.Slug,
            IdPrefix.EncodeMember(m.Id),
            m.Status,
            m.Tenant.OwnerMemberId == m.Id,
            m.JoinedAt
        )).ToList();
    }
}
