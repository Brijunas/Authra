namespace Authra.Domain.Entities;

/// <summary>
/// Base class for tenant-scoped entities with RLS support.
/// All entities inheriting from this will have TenantId for Row-Level Security.
/// </summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; protected set; }
}
