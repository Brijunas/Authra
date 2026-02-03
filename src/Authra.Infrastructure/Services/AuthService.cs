using System.Security.Cryptography;
using System.Text;
using Authra.Application.Auth;
using Authra.Application.Auth.DTOs;
using Authra.Application.Common.Interfaces;
using Authra.Domain.Entities;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Authentication service implementation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuthService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailSender emailSender,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // Check if email already exists
        var emailExists = await _context.UserIdentifiers
            .AnyAsync(ui => ui.Type == "email" && ui.ValueNormalized == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new ConflictException("Email is already registered");
        }

        // Check username if provided
        string? normalizedUsername = null;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            normalizedUsername = request.Username.ToLowerInvariant().Trim();
            var usernameExists = await _context.UserIdentifiers
                .AnyAsync(ui => ui.Type == "username" && ui.ValueNormalized == normalizedUsername, cancellationToken);

            if (usernameExists)
            {
                throw new ConflictException("Username is already taken");
            }
        }

        // Create user
        var user = User.Create();
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Add email identifier
        user.AddIdentifier("email", request.Email);

        // Add username identifier if provided
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            user.AddIdentifier("username", request.Username);
        }

        // Hash password and create PasswordAuth
        var passwordHash = _passwordHasher.Hash(request.Password);
        var passwordAuth = PasswordAuth.Create(user.Id, passwordHash, "argon2id");
        _context.PasswordAuths.Add(passwordAuth);

        await _context.SaveChangesAsync(cancellationToken);

        return new RegisterResponse(
            PrefixId("usr", user.Id),
            normalizedEmail,
            normalizedUsername);
    }

    public async Task<OneOf<LoginResponse, TenantSelectionRequired>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // Find user by email
        var identifier = await _context.UserIdentifiers
            .Include(ui => ui.User)
                .ThenInclude(u => u.PasswordAuth)
            .FirstOrDefaultAsync(ui => ui.Type == "email" && ui.ValueNormalized == normalizedEmail, cancellationToken);

        if (identifier?.User.PasswordAuth == null)
        {
            throw AuthenticationException.InvalidCredentials();
        }

        var user = identifier.User;

        // Verify password
        var verifyResult = _passwordHasher.Verify(request.Password, user.PasswordAuth.PasswordHash);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw AuthenticationException.InvalidCredentials();
        }

        // Re-hash if needed (algorithm upgrade)
        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = _passwordHasher.Hash(request.Password);
            user.PasswordAuth.UpdatePassword(newHash, "argon2id");
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Get user's tenant memberships
        var memberships = await _context.TenantMembers
            .Include(tm => tm.Tenant)
            .Where(tm => tm.UserId == user.Id && tm.Status == "active")
            .Where(tm => tm.Tenant.Status == "active")
            .ToListAsync(cancellationToken);

        // If specific tenant requested
        if (!string.IsNullOrEmpty(request.TenantId))
        {
            var tenantId = ParsePrefixedId(request.TenantId, "tnt");
            var membership = memberships.FirstOrDefault(m => m.TenantId == tenantId);

            if (membership == null)
            {
                throw new ForbiddenException("You do not have access to this tenant");
            }

            return await BuildLoginResponseAsync(user, membership, cancellationToken);
        }

        // If user has no tenants, return login without tenant context
        if (memberships.Count == 0)
        {
            // User registered but hasn't joined any tenant
            var userInfo = await GetUserInfoAsync(user.Id, cancellationToken);
            return new LoginResponse(
                AccessToken: string.Empty,
                RefreshToken: string.Empty,
                AccessTokenExpiresAt: default,
                RefreshTokenExpiresAt: default,
                User: userInfo,
                Tenant: null);
        }

        // If user has exactly one tenant, auto-select it
        if (memberships.Count == 1)
        {
            return await BuildLoginResponseAsync(user, memberships[0], cancellationToken);
        }

        // Multiple tenants - return selection required
        var availableTenants = memberships
            .Select(m => new AvailableTenant(
                PrefixId("tnt", m.TenantId),
                m.Tenant.Name,
                m.Tenant.Slug))
            .ToList();

        return new TenantSelectionRequired(
            PrefixId("usr", user.Id),
            availableTenants);
    }

    public async Task<LoginResponse> CompleteLoginAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var membership = await _context.TenantMembers
            .Include(tm => tm.Tenant)
            .FirstOrDefaultAsync(tm => tm.UserId == userId && tm.TenantId == tenantId, cancellationToken)
            ?? throw new ForbiddenException("You do not have access to this tenant");

        if (!membership.IsActive)
        {
            throw AuthenticationException.AccountSuspended();
        }

        return await BuildLoginResponseAsync(user, membership, cancellationToken);
    }

    public async Task<RefreshResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenPair = await _tokenService.RefreshTokensAsync(request.RefreshToken, cancellationToken);

        return new RefreshResponse(
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresAt,
            tokenPair.RefreshTokenExpiresAt);
    }

    public async Task<LoginResponse> SwitchTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await CompleteLoginAsync(userId, tenantId, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(PasswordResetRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        var identifier = await _context.UserIdentifiers
            .Include(ui => ui.User)
            .FirstOrDefaultAsync(ui => ui.Type == "email" && ui.ValueNormalized == normalizedEmail, cancellationToken);

        // Always return success to prevent email enumeration
        if (identifier == null)
        {
            return;
        }

        // Invalidate any existing reset tokens
        var existingTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == identifier.UserId && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        foreach (var token in existingTokens)
        {
            if (token.IsValid(now))
            {
                token.MarkAsUsed(now);
            }
        }

        // Generate new token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenString = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenString));

        var resetToken = PasswordResetToken.Create(
            identifier.UserId,
            tokenHash,
            now.Add(PasswordResetToken.DefaultExpiration));

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Send email
        await _emailSender.SendAsync(new EmailMessage(
            To: normalizedEmail,
            Subject: "Reset your password",
            HtmlBody: $"<p>Click the link to reset your password: <a href=\"https://authra.io/reset-password?token={tokenString}\">Reset Password</a></p><p>This link expires in 1 hour.</p>",
            TextBody: $"Reset your password by visiting: https://authra.io/reset-password?token={tokenString}\n\nThis link expires in 1 hour."),
            cancellationToken);
    }

    public async Task ResetPasswordAsync(PasswordResetDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token));
        var now = _dateTimeProvider.UtcNow;

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
                .ThenInclude(u => u.PasswordAuth)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken == null || !resetToken.IsValid(now))
        {
            throw new ValidationException("Invalid or expired reset token");
        }

        // Mark token as used
        resetToken.MarkAsUsed(now);

        // Update password
        var newHash = _passwordHasher.Hash(request.NewPassword);
        if (resetToken.User.PasswordAuth != null)
        {
            resetToken.User.PasswordAuth.UpdatePassword(newHash, "argon2id");
        }
        else
        {
            var passwordAuth = PasswordAuth.Create(resetToken.UserId, newHash, "argon2id");
            _context.PasswordAuths.Add(passwordAuth);
        }

        // Revoke all refresh tokens for security
        var refreshTokens = await _context.RefreshTokens
            .Where(t => t.UserId == resetToken.UserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in refreshTokens)
        {
            token.RevokeForPasswordChange(now);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, Guid tenantId, string? refreshToken, string? accessToken, bool logoutAll, CancellationToken cancellationToken = default)
    {
        if (logoutAll)
        {
            await _tokenService.RevokeAllUserTokensAsync(userId, tenantId, "logout", cancellationToken);
        }
        else if (!string.IsNullOrEmpty(refreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken, "logout", cancellationToken);
        }

        // Blacklist the current access token if provided
        if (!string.IsNullOrEmpty(accessToken))
        {
            await _tokenService.BlacklistAccessTokenAsync(accessToken, "logout", cancellationToken);
        }
    }

    private async Task<LoginResponse> BuildLoginResponseAsync(User user, TenantMember membership, CancellationToken cancellationToken)
    {
        // Load roles and permissions
        var roleAssignments = await _context.TenantMemberRoles
            .Include(tmr => tmr.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(tmr => tmr.TenantMemberId == membership.Id)
            .ToListAsync(cancellationToken);

        var roles = roleAssignments.Select(ra => ra.Role.Code).ToList();
        var permissions = roleAssignments
            .SelectMany(ra => ra.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        // Load organization memberships
        var orgMemberships = await _context.OrganizationMembers
            .Where(om => om.TenantMemberId == membership.Id)
            .Select(om => om.OrganizationId)
            .ToListAsync(cancellationToken);

        // Generate tokens
        var tokenClaims = new TokenClaims(
            user.Id,
            membership.TenantId,
            membership.Id,
            orgMemberships,
            roles,
            permissions);

        var tokenPair = await _tokenService.GenerateTokenPairAsync(tokenClaims, cancellationToken);

        // Build response
        var userInfo = await GetUserInfoAsync(user.Id, cancellationToken);
        var tenantInfo = new TenantInfo(
            PrefixId("tnt", membership.TenantId),
            membership.Tenant.Name,
            membership.Tenant.Slug,
            PrefixId("mbr", membership.Id),
            roles,
            permissions);

        return new LoginResponse(
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresAt,
            tokenPair.RefreshTokenExpiresAt,
            userInfo,
            tenantInfo);
    }

    private async Task<UserInfo> GetUserInfoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var identifiers = await _context.UserIdentifiers
            .Where(ui => ui.UserId == userId)
            .ToListAsync(cancellationToken);

        var email = identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized ?? "";
        var username = identifiers.FirstOrDefault(i => i.Type == "username")?.ValueNormalized;

        return new UserInfo(PrefixId("usr", userId), email, username);
    }

    private static string PrefixId(string prefix, Guid id) => $"{prefix}_{id:N}";

    private static Guid ParsePrefixedId(string prefixedId, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(prefixedId))
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID format");
        }

        var parts = prefixedId.Split('_', 2);
        if (parts.Length != 2 || parts[0] != expectedPrefix)
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID format");
        }

        if (!Guid.TryParse(parts[1], out var id))
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID format");
        }

        return id;
    }
}
