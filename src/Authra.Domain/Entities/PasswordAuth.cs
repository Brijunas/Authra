namespace Authra.Domain.Entities;

/// <summary>
/// Password credentials for a user.
/// Global entity with UNIQUE(UserId) - one password per user.
/// Uses PHC string format for self-describing password hashes.
/// </summary>
public class PasswordAuth : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// PHC-formatted password hash containing algorithm, parameters, salt, and hash.
    /// Example: $argon2id$v=19$m=47104,t=1,p=1$base64salt$base64hash
    /// </summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// Hashing algorithm used: 'argon2id', 'bcrypt', 'scrypt'.
    /// </summary>
    public string Algorithm { get; private set; } = null!;

    /// <summary>
    /// Algorithm-specific parameters as JSON (optional, for non-PHC formats).
    /// </summary>
    public string? Params { get; private set; }

    private PasswordAuth()
    {
        // EF Core constructor
    }

    public static PasswordAuth Create(Guid userId, string passwordHash, string algorithm, string? @params = null)
    {
        ValidateAlgorithm(algorithm);

        return new PasswordAuth
        {
            UserId = userId,
            PasswordHash = passwordHash,
            Algorithm = algorithm.ToLowerInvariant(),
            Params = @params
        };
    }

    public void UpdatePassword(string newPasswordHash, string algorithm, string? @params = null)
    {
        ValidateAlgorithm(algorithm);

        PasswordHash = newPasswordHash;
        Algorithm = algorithm.ToLowerInvariant();
        Params = @params;
    }

    private static void ValidateAlgorithm(string algorithm)
    {
        var validAlgorithms = new[] { "argon2id", "bcrypt", "scrypt" };
        if (!validAlgorithms.Contains(algorithm.ToLowerInvariant()))
        {
            throw new ArgumentException($"Invalid algorithm: {algorithm}. Must be one of: {string.Join(", ", validAlgorithms)}", nameof(algorithm));
        }
    }
}
