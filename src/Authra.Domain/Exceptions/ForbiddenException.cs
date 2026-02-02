namespace Authra.Domain.Exceptions;

/// <summary>
/// Exception thrown when a user lacks permission for an operation.
/// </summary>
public class ForbiddenException : DomainException
{
    public override int StatusCode => 403;

    public string? RequiredPermission { get; }

    public ForbiddenException(string message) : base(message)
    {
    }

    private ForbiddenException(string message, string permission) : base(message)
    {
        RequiredPermission = permission;
    }

    public static ForbiddenException ForPermission(string permission)
        => new($"You do not have the required permission: {permission}", permission);
}
