namespace Authra.Domain.Exceptions;

/// <summary>
/// Exception thrown when an operation conflicts with existing state.
/// </summary>
public class ConflictException : DomainException
{
    public override int StatusCode => 409;

    public ConflictException(string message) : base(message)
    {
    }
}
