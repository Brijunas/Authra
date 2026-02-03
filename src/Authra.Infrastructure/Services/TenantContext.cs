using Authra.Application.Common.Interfaces;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Provides access to the current tenant context.
/// Thread-safe using AsyncLocal for request-scoped state.
/// </summary>
public class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantContextData> _current = new();

    public Guid? TenantId => _current.Value?.TenantId;
    public Guid? TenantMemberId => _current.Value?.TenantMemberId;
    public Guid? UserId => _current.Value?.UserId;

    public void SetTenant(Guid tenantId, Guid tenantMemberId)
    {
        _current.Value = new TenantContextData(tenantId, tenantMemberId, _current.Value?.UserId);
    }

    public void SetUser(Guid userId)
    {
        _current.Value = new TenantContextData(_current.Value?.TenantId, _current.Value?.TenantMemberId, userId);
    }

    public void Clear()
    {
        _current.Value = default;
    }

    private record TenantContextData(Guid? TenantId, Guid? TenantMemberId, Guid? UserId);
}
