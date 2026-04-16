---
name: security-practices
description: "Security patterns for authentication, authorization, and cryptography in Authra — JWT ES256, refresh token rotation, Argon2id password hashing, and permission checks."
when_to_use: "When implementing JWT token issuance, refresh token rotation, Argon2id password hashing, or permission checks."
paths:
  - "src/Authra.Api/Endpoints/Auth*/**"
  - "src/Authra.Application/Auth/**/*"
  - "src/Authra.Infrastructure/Services/*Password*.cs"
  - "src/Authra.Infrastructure/Services/*Token*.cs"
  - "src/Authra.Infrastructure/Services/*Jwt*.cs"
version: 2.2.0
---

# Authra Security Conventions

This skill defines security patterns for authentication, authorization, and cryptography in Authra.

## Context7 Usage

When implementing security features, use Context7 MCP tools to query:
- `/dotnet/aspnetcore` - ASP.NET Core authentication/authorization
- Query "JWT" or "JsonWebToken" for token handling patterns

## JWT Access Tokens

### Token Structure

Access tokens use ES256 (ECDSA P-256) signing:

```csharp
public class TokenService(IOptions<JwtSettings> settings)
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string GenerateAccessToken(User user, TenantMember member)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tid", member.TenantId.ToString()),
            new Claim("mid", member.Id.ToString()),
            new Claim("oid", string.Join(",", member.OrganizationIds)),
            new Claim("roles", string.Join(",", member.RoleCodes)),
            new Claim("permissions", string.Join(",", member.Permissions))
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = settings.Value.Issuer,
            Audience = settings.Value.Audience,
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(_key),
                SecurityAlgorithms.EcdsaSha256)
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }
}
```

### Standard Claims

| Claim | Description |
|-------|-------------|
| `sub` | User ID (global identity) |
| `tid` | Current Tenant ID |
| `mid` | TenantMember ID |
| `oid` | Organization IDs (comma-separated) |
| `roles` | Role codes in current tenant |
| `permissions` | Flattened permission codes |

### Token Lifetime

- Access token: 15 minutes
- Refresh token: 30 days sliding, 90 days absolute

## Refresh Tokens

### Opaque Token Generation

```csharp
public string GenerateRefreshToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Convert.ToBase64String(bytes);
}
```

### Token Storage

Store hashed tokens, never plaintext:

```csharp
public class RefreshToken
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public string TokenHash { get; init; } = string.Empty; // SHA-256 hash
    public string FamilyId { get; init; } = string.Empty;  // Rotation tracking
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
```

### Token Rotation

Rotate refresh tokens on each use:

```csharp
public async Task<TokenPair> RefreshAsync(string refreshToken)
{
    var hash = ComputeSha256Hash(refreshToken);
    var token = await _context.RefreshTokens
        .FirstOrDefaultAsync(t => t.TokenHash == hash);

    if (token is null || token.RevokedAt.HasValue)
        throw new SecurityException("Invalid refresh token");

    if (token.ExpiresAt < DateTime.UtcNow)
        throw new SecurityException("Refresh token expired");

    // Revoke old token
    token.RevokedAt = DateTime.UtcNow;

    // Generate new token in same family
    var newToken = GenerateRefreshToken();
    token.ReplacedByTokenHash = ComputeSha256Hash(newToken);

    // Create new refresh token record
    var newRefreshToken = new RefreshToken
    {
        UserId = token.UserId,
        TenantId = token.TenantId,
        TokenHash = ComputeSha256Hash(newToken),
        FamilyId = token.FamilyId,
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };

    _context.RefreshTokens.Add(newRefreshToken);
    await _context.SaveChangesAsync();

    return new TokenPair(GenerateAccessToken(...), newToken);
}
```

### Reuse Detection

Detect token reuse (indicates theft):

```csharp
if (token.RevokedAt.HasValue)
{
    // Token was already used - potential theft
    // Revoke entire family
    await RevokeTokenFamilyAsync(token.FamilyId);
    throw new SecurityException("Token reuse detected");
}
```

## Password Hashing

### Argon2id Implementation

```csharp
public class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemorySize = 47104; // 46 MiB
    private const int Iterations = 1;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };

        var hash = argon2.GetBytes(HashSize);

        // PHC string format
        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public PasswordVerificationResult Verify(string password, string hashedPassword)
    {
        var parts = ParsePhcString(hashedPassword);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = parts.Salt,
            MemorySize = parts.MemorySize,
            Iterations = parts.Iterations,
            DegreeOfParallelism = parts.Parallelism
        };

        var computedHash = argon2.GetBytes(HashSize);

        if (!CryptographicOperations.FixedTimeEquals(computedHash, parts.Hash))
            return PasswordVerificationResult.Failed;

        if (NeedsRehash(hashedPassword))
            return PasswordVerificationResult.SuccessRehashNeeded;

        return PasswordVerificationResult.Success;
    }

    public bool NeedsRehash(string hashedPassword)
    {
        var parts = ParsePhcString(hashedPassword);
        return parts.MemorySize != MemorySize
            || parts.Iterations != Iterations
            || parts.Parallelism != Parallelism;
    }
}
```

### PHC String Format

```
$argon2id$v=19$m=47104,t=1,p=1$<base64-salt>$<base64-hash>
```

Self-describing format enables algorithm migration without schema changes.

## Permission Checking

### Permission Format

Colon notation: `{action}:{resource}[.{subresource}]`

Examples: `accounts:read`, `roles:assign`, `organizations:members.write`

### Authorization Attribute

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
```

### Authorization Handler

```csharp
public class PermissionAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var endpoint = context.Resource as Endpoint;
        var attribute = endpoint?.Metadata.GetMetadata<RequirePermissionAttribute>();

        if (attribute is null)
            return Task.CompletedTask;

        var permissions = context.User
            .FindFirst("permissions")?.Value?
            .Split(',') ?? [];

        if (permissions.Contains(attribute.Permission))
            context.Succeed(context.Requirements.First());
        else
            context.Fail();

        return Task.CompletedTask;
    }
}
```

### Endpoint Usage

```csharp
group.MapGet("/", GetAllUsersAsync)
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("accounts:read"));
```

## Security Headers

Configure security headers middleware:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    await next();
});
```

## Secure Random Generation

Always use `RandomNumberGenerator`:

```csharp
// Good
var bytes = RandomNumberGenerator.GetBytes(32);
var token = Convert.ToBase64String(bytes);

// Bad - predictable
var random = new Random();
```

## Sensitive Data Handling

Never log sensitive data:

```csharp
// Good
logger.LogInformation("Login attempt for {Email}", email);

// Bad
logger.LogInformation("Login with password {Password}", password);
```

Never return sensitive data in responses:

```csharp
// Good - DTO without sensitive fields
public record UserDto(Guid Id, string Email, DateTime CreatedAt);

// Bad - includes password hash
public record UserDto(Guid Id, string Email, string PasswordHash);
```
