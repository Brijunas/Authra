namespace Authra.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : DomainException
{
    public override int StatusCode => 404;

    public NotFoundException(string entity, object id)
        : base($"{entity} with ID '{id}' was not found")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
