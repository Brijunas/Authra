namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Result of password verification.
/// </summary>
public enum PasswordVerificationResult
{
    /// <summary>
    /// Password verification failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Password verification succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// Password verification succeeded but the hash should be upgraded.
    /// </summary>
    SuccessRehashNeeded
}

/// <summary>
/// Abstraction for password hashing to support algorithm flexibility.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password using the current algorithm and parameters.
    /// Returns a PHC-formatted string containing algorithm, parameters, salt, and hash.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    PasswordVerificationResult Verify(string password, string hashedPassword);

    /// <summary>
    /// Checks if a hash needs to be upgraded due to algorithm or parameter changes.
    /// </summary>
    bool NeedsRehash(string hashedPassword);
}
