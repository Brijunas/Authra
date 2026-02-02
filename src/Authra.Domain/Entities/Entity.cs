namespace Authra.Domain.Entities;

/// <summary>
/// Base class for all entities with UUID v7 primary key.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }
}
