using System.Security.Cryptography;
using System.Text;
using Authra.Application.Common.Interfaces;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Argon2id password hasher with PHC string format output.
/// Default parameters: m=47104 (46 MiB), t=1, p=1
/// </summary>
public class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128-bit
    private const int HashSize = 32; // 256-bit
    private const string Algorithm = "argon2id";
    private const int Version = 19; // 0x13

    private readonly PasswordHashingOptions _options;

    public Argon2PasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Hashes a password using Argon2id with PHC string format.
    /// Format: $argon2id$v=19$m=47104,t=1,p=1$base64salt$base64hash
    /// </summary>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, _options.MemoryCost, _options.TimeCost, _options.Parallelism);

        return FormatPhcString(salt, hash, _options.MemoryCost, _options.TimeCost, _options.Parallelism);
    }

    /// <summary>
    /// Verifies a password against a PHC-formatted hash.
    /// </summary>
    public PasswordVerificationResult Verify(string password, string hashedPassword)
    {
        if (!TryParsePhcString(hashedPassword, out var salt, out var storedHash, out var m, out var t, out var p))
        {
            return PasswordVerificationResult.Failed;
        }

        var computedHash = ComputeHash(password, salt, m, t, p);

        if (!CryptographicOperations.FixedTimeEquals(computedHash, storedHash))
        {
            return PasswordVerificationResult.Failed;
        }

        // Check if rehash is needed due to parameter changes
        if (NeedsRehash(m, t, p))
        {
            return PasswordVerificationResult.SuccessRehashNeeded;
        }

        return PasswordVerificationResult.Success;
    }

    /// <summary>
    /// Checks if a hash needs to be upgraded due to parameter changes.
    /// </summary>
    public bool NeedsRehash(string hashedPassword)
    {
        if (!TryParsePhcString(hashedPassword, out _, out _, out var m, out var t, out var p))
        {
            return true; // Unknown format, needs rehash
        }

        return NeedsRehash(m, t, p);
    }

    private bool NeedsRehash(int memoryCost, int timeCost, int parallelism)
    {
        return memoryCost != _options.MemoryCost ||
               timeCost != _options.TimeCost ||
               parallelism != _options.Parallelism;
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryCost, int timeCost, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryCost,
            Iterations = timeCost,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(HashSize);
    }

    /// <summary>
    /// Formats hash as PHC string: $argon2id$v=19$m={m},t={t},p={p}${salt}${hash}
    /// </summary>
    private static string FormatPhcString(byte[] salt, byte[] hash, int m, int t, int p)
    {
        var saltBase64 = Convert.ToBase64String(salt).TrimEnd('=');
        var hashBase64 = Convert.ToBase64String(hash).TrimEnd('=');

        return $"${Algorithm}$v={Version}$m={m},t={t},p={p}${saltBase64}${hashBase64}";
    }

    /// <summary>
    /// Parses a PHC string into its components.
    /// </summary>
    private static bool TryParsePhcString(string phcString, out byte[] salt, out byte[] hash, out int m, out int t, out int p)
    {
        salt = [];
        hash = [];
        m = t = p = 0;

        if (string.IsNullOrEmpty(phcString) || !phcString.StartsWith($"${Algorithm}$"))
        {
            return false;
        }

        var parts = phcString.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return false;
        }

        // parts[0] = "argon2id"
        // parts[1] = "v=19"
        // parts[2] = "m=47104,t=1,p=1"
        // parts[3] = base64 salt
        // parts[4] = base64 hash

        if (!parts[1].StartsWith("v="))
        {
            return false;
        }

        // Parse parameters
        var paramParts = parts[2].Split(',');
        foreach (var param in paramParts)
        {
            var kv = param.Split('=');
            if (kv.Length != 2)
            {
                return false;
            }

            switch (kv[0])
            {
                case "m":
                    if (!int.TryParse(kv[1], out m)) return false;
                    break;
                case "t":
                    if (!int.TryParse(kv[1], out t)) return false;
                    break;
                case "p":
                    if (!int.TryParse(kv[1], out p)) return false;
                    break;
            }
        }

        if (m == 0 || t == 0 || p == 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(PadBase64(parts[3]));
            hash = Convert.FromBase64String(PadBase64(parts[4]));
        }
        catch
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    /// <summary>
    /// Pads base64 string with = characters if needed.
    /// </summary>
    private static string PadBase64(string base64)
    {
        var padding = (4 - base64.Length % 4) % 4;
        return base64 + new string('=', padding);
    }
}

/// <summary>
/// Configuration options for password hashing.
/// </summary>
public class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    /// <summary>
    /// Memory cost in KiB. Default: 47104 (46 MiB).
    /// </summary>
    public int MemoryCost { get; set; } = 47104;

    /// <summary>
    /// Number of iterations. Default: 1.
    /// </summary>
    public int TimeCost { get; set; } = 1;

    /// <summary>
    /// Degree of parallelism. Default: 1.
    /// </summary>
    public int Parallelism { get; set; } = 1;
}
