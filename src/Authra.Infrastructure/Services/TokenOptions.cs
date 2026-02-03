namespace Authra.Infrastructure.Services;

/// <summary>
/// Configuration options for JWT token generation.
/// </summary>
public class TokenOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Token issuer (iss claim). Example: "https://authra.io"
    /// </summary>
    public string Issuer { get; set; } = "https://authra.io";

    /// <summary>
    /// Token audience (aud claim). Example: "authra-api"
    /// </summary>
    public string Audience { get; set; } = "authra-api";

    /// <summary>
    /// Access token lifetime in minutes. Default: 15.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token sliding window in days. Default: 30.
    /// </summary>
    public int RefreshTokenSlidingDays { get; set; } = 30;

    /// <summary>
    /// Refresh token absolute maximum lifetime in days. Default: 90.
    /// </summary>
    public int RefreshTokenAbsoluteDays { get; set; } = 90;

    /// <summary>
    /// Signing key lifetime in days. Default: 90.
    /// </summary>
    public int SigningKeyLifetimeDays { get; set; } = 90;
}
