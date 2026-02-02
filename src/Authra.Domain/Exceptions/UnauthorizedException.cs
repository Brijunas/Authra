namespace Authra.Domain.Exceptions;

/// <summary>
/// Exception thrown when authentication fails or credentials are invalid.
/// </summary>
public class UnauthorizedException : DomainException
{
    public override int StatusCode => 401;

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException() : base("Invalid credentials")
    {
    }
}
