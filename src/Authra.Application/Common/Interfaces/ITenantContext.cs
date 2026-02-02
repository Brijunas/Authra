namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current tenant context for multi-tenant operations.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID. Returns null if no tenant context is set.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Gets the current tenant member ID. Returns null if no tenant context is set.
    /// </summary>
    Guid? TenantMemberId { get; }

    /// <summary>
    /// Gets the current user ID. Returns null if not authenticated.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Sets the tenant context for the current request.
    /// </summary>
    void SetTenant(Guid tenantId, Guid tenantMemberId);
}
