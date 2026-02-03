namespace Authra.Domain.Entities;

/// <summary>
/// JWT signing key for token generation and rotation.
/// Global entity - not tenant-scoped.
/// Uses human-readable KeyId (e.g., 'key-2026-01-25-001') instead of UUID.
/// </summary>
public class SigningKey
{
    /// <summary>
    /// Human-readable key identifier (e.g., 'key-2026-01-25-001').
    /// </summary>
    public string KeyId { get; private set; } = string.Empty;

    /// <summary>
    /// Signing algorithm: ES256, ES384, or RS256.
    /// </summary>
    public string Algorithm { get; private set; } = "ES256";

    /// <summary>
    /// Public key in PEM format (for JWKS endpoint).
    /// </summary>
    public string PublicKeyPem { get; private set; } = string.Empty;

    /// <summary>
    /// Private key encrypted with KMS. Never expose in plaintext.
    /// </summary>
    public byte[] PrivateKeyEncrypted { get; private set; } = null!;

    /// <summary>
    /// Key lifecycle status: pending, active, rotate_out, expired.
    /// </summary>
    public string Status { get; private set; } = "pending";

    /// <summary>
    /// When the key was activated for signing.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>
    /// When the key was marked for rotation (still valid for verification).
    /// </summary>
    public DateTimeOffset? RotatedOutAt { get; private set; }

    /// <summary>
    /// When the key expires (no longer valid for verification).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// When the key was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private SigningKey()
    {
        // EF Core constructor
    }

    public static SigningKey Create(
        string keyId,
        string publicKeyPem,
        byte[] privateKeyEncrypted,
        DateTimeOffset expiresAt,
        string algorithm = "ES256")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        ArgumentNullException.ThrowIfNull(privateKeyEncrypted);

        ValidateAlgorithm(algorithm);

        return new SigningKey
        {
            KeyId = keyId,
            Algorithm = algorithm,
            PublicKeyPem = publicKeyPem,
            PrivateKeyEncrypted = privateKeyEncrypted,
            Status = "pending",
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Activate(DateTimeOffset activatedAt)
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot activate key in '{Status}' status. Only 'pending' keys can be activated.");

        Status = "active";
        ActivatedAt = activatedAt;
    }

    public void RotateOut(DateTimeOffset rotatedOutAt)
    {
        if (Status != "active")
            throw new InvalidOperationException($"Cannot rotate out key in '{Status}' status. Only 'active' keys can be rotated out.");

        Status = "rotate_out";
        RotatedOutAt = rotatedOutAt;
    }

    public void Expire()
    {
        if (Status == "expired")
            return;

        Status = "expired";
    }

    public bool IsActive => Status == "active";
    public bool CanVerify => Status is "active" or "rotate_out";
    public bool IsExpired => Status == "expired";

    private static void ValidateAlgorithm(string algorithm)
    {
        if (algorithm is not ("ES256" or "ES384" or "RS256"))
            throw new ArgumentException($"Invalid algorithm '{algorithm}'. Supported: ES256, ES384, RS256.", nameof(algorithm));
    }
}
