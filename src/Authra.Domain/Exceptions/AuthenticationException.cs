namespace Authra.Domain.Exceptions;

/// <summary>
/// Exception thrown when authentication fails (login, token refresh, etc.).
/// Returns 401 Unauthorized status code.
/// </summary>
public class AuthenticationException : DomainException
{
    public override int StatusCode => 401;

    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException() : base("Authentication failed")
    {
    }

    public static AuthenticationException InvalidCredentials()
        => new("Invalid email or password");

    public static AuthenticationException InvalidToken()
        => new("Invalid or expired token");

    public static AuthenticationException AccountSuspended()
        => new("Account is suspended");

    public static AuthenticationException AccountRemoved()
        => new("Account has been removed");

    public static AuthenticationException TokenExpired()
        => new("Token has expired");

    public static AuthenticationException TokenReuseDetected()
        => new("Token reuse detected. All sessions revoked for security.");
}
