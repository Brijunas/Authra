using System.Security.Claims;
using Authra.Domain.Exceptions;

namespace Authra.Api.Infrastructure;

/// <summary>
/// Helper for extracting claims from the current user.
/// </summary>
public static class ClaimsHelper
{
    private const string TenantIdClaim = "tid";
    private const string MemberIdClaim = "mid";
    private const string PermissionsClaim = "permissions";

    /// <summary>
    /// Gets the user ID from claims.
    /// </summary>
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedException("User ID not found in token");

        return Guid.Parse(sub);
    }

    /// <summary>
    /// Gets the tenant ID from claims.
    /// </summary>
    public static Guid GetTenantId(ClaimsPrincipal user)
    {
        var tid = user.FindFirstValue(TenantIdClaim)
            ?? throw new UnauthorizedException("Tenant ID not found in token");

        return Guid.Parse(tid);
    }

    /// <summary>
    /// Gets the member ID from claims.
    /// </summary>
    public static Guid GetMemberId(ClaimsPrincipal user)
    {
        var mid = user.FindFirstValue(MemberIdClaim)
            ?? throw new UnauthorizedException("Member ID not found in token");

        return Guid.Parse(mid);
    }

    /// <summary>
    /// Gets the list of permissions from claims.
    /// </summary>
    public static IReadOnlySet<string> GetPermissions(ClaimsPrincipal user)
    {
        return user.FindAll(PermissionsClaim)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the user has a specific permission.
    /// </summary>
    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return GetPermissions(user).Contains(permission);
    }

    /// <summary>
    /// Validates that the tenant ID in the URL matches the user's current tenant context.
    /// </summary>
    public static void ValidateTenantAccess(ClaimsPrincipal user, Guid tenantId)
    {
        var userTenantId = GetTenantId(user);
        if (userTenantId != tenantId)
        {
            throw new ForbiddenException("You do not have access to this tenant");
        }
    }
}
