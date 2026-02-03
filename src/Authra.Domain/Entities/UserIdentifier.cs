namespace Authra.Domain.Entities;

/// <summary>
/// Login identifiers for a user (email, username, phone).
/// Global entity with UNIQUE(Type, ValueNormalized) constraint.
/// </summary>
public class UserIdentifier : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// Identifier type: 'email', 'username', or 'phone'.
    /// </summary>
    public string Type { get; private set; } = null!;

    /// <summary>
    /// Normalized value for case-insensitive lookups.
    /// Email/username are lowercased, phone numbers have non-digits removed.
    /// </summary>
    public string ValueNormalized { get; private set; } = null!;

    private UserIdentifier()
    {
        // EF Core constructor
    }

    internal static UserIdentifier Create(Guid userId, string type, string valueNormalized)
    {
        ValidateType(type);

        return new UserIdentifier
        {
            UserId = userId,
            Type = type.ToLowerInvariant(),
            ValueNormalized = valueNormalized
        };
    }

    private static void ValidateType(string type)
    {
        var validTypes = new[] { "email", "username", "phone" };
        if (!validTypes.Contains(type.ToLowerInvariant()))
        {
            throw new ArgumentException($"Invalid identifier type: {type}. Must be one of: {string.Join(", ", validTypes)}", nameof(type));
        }
    }
}
