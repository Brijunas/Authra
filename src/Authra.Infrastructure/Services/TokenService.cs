using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Authra.Application.Common.Interfaces;
using Authra.Domain.Entities;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Authra.Infrastructure.Services;

/// <summary>
/// JWT access token and opaque refresh token service.
/// Uses ES256 signing with key rotation support.
/// </summary>
public class TokenService : ITokenService
{
    private readonly AppDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TokenOptions _options;

    // Custom claim names
    private const string TenantIdClaim = "tid";
    private const string MemberIdClaim = "mid";
    private const string OrganizationIdsClaim = "oid";
    private const string RolesClaim = "roles";
    private const string PermissionsClaim = "permissions";

    public TokenService(
        AppDbContext context,
        IDateTimeProvider dateTimeProvider,
        IOptions<TokenOptions> options)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
    }

    public async Task<UserOnlyAccessToken> GenerateUserOnlyAccessTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var accessTokenExpires = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        // Get or create active signing key
        var signingKey = await GetOrCreateActiveSigningKeyAsync(cancellationToken);

        // Generate user-only access token (no tenant claims)
        // Note: ECDsa is not disposed here because ECDsaSecurityKey may hold a reference
        // and JsonWebTokenHandler.CreateToken may use it lazily. The key will be GC'd.
        var ecdsa = LoadEcdsaPrivateKey(signingKey);
        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = signingKey.KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(jwtClaims),
            NotBefore = now.UtcDateTime,
            Expires = accessTokenExpires.UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        var accessToken = handler.CreateToken(tokenDescriptor);

        return new UserOnlyAccessToken(accessToken, accessTokenExpires);
    }

    public async Task<TokenPair> GenerateTokenPairAsync(TokenClaims claims, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var accessTokenExpires = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var refreshTokenExpires = now.AddDays(_options.RefreshTokenSlidingDays);
        var absoluteExpires = now.AddDays(_options.RefreshTokenAbsoluteDays);

        // Get or create active signing key
        var signingKey = await GetOrCreateActiveSigningKeyAsync(cancellationToken);

        // Generate access token (JWT)
        var accessToken = GenerateAccessToken(claims, signingKey, now, accessTokenExpires);

        // Generate refresh token (opaque)
        var (refreshToken, refreshTokenHash) = GenerateRefreshToken();

        // Store refresh token
        var refreshTokenEntity = RefreshToken.Create(
            tokenHash: refreshTokenHash,
            userId: claims.UserId,
            tenantId: claims.TenantId,
            tenantMemberId: claims.TenantMemberId,
            expiresAt: refreshTokenExpires,
            absoluteExpiresAt: absoluteExpires);

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        return new TokenPair(
            accessToken,
            refreshToken,
            accessTokenExpires,
            refreshTokenExpires);
    }

    public async Task<TokenClaims?> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var handler = new JsonWebTokenHandler();

        // Get all valid signing keys (active + rotate_out)
        var validKeys = await _context.SigningKeys
            .Where(k => k.Status == "active" || k.Status == "rotate_out")
            .ToListAsync(cancellationToken);

        if (validKeys.Count == 0)
        {
            return null;
        }

        var ecdsaKeys = validKeys.Select(k => LoadEcdsaPublicKey(k)).ToArray();

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKeys = ecdsaKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var result = await handler.ValidateTokenAsync(accessToken, validationParameters);

            if (!result.IsValid)
            {
                return null;
            }

            // Check blacklist
            var jti = result.Claims[JwtRegisteredClaimNames.Jti]?.ToString();
            if (!string.IsNullOrEmpty(jti))
            {
                var isBlacklisted = await _context.TokenBlacklist
                    .AnyAsync(t => t.Jti == jti, cancellationToken);

                if (isBlacklisted)
                {
                    return null;
                }
            }

            return ExtractClaims(result.Claims);
        }
        catch
        {
            return null;
        }
        finally
        {
            // ECDsaSecurityKey wraps ECDsa, dispose the underlying key
            foreach (var key in ecdsaKeys)
            {
                (key as IDisposable)?.Dispose();
            }
        }
    }

    public async Task<TokenPair> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var tokenHash = ComputeTokenHash(refreshToken);

        // Find the refresh token
        var storedToken = await _context.RefreshTokens
            .Include(t => t.TenantMember)
                .ThenInclude(m => m.RoleAssignments)
                    .ThenInclude(ra => ra.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .Include(t => t.TenantMember)
                .ThenInclude(m => m.OrganizationMemberships)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null)
        {
            throw new AuthenticationException("Invalid refresh token.");
        }

        // Check if token is revoked (potential reuse attack)
        if (storedToken.IsRevoked)
        {
            // Token reuse detected - revoke entire family
            await RevokeFamilyAsync(storedToken.FamilyId, "reuse_detected", cancellationToken);
            throw new AuthenticationException("Token reuse detected. All sessions revoked for security.");
        }

        // Check expiration
        if (storedToken.IsExpired(now))
        {
            throw new AuthenticationException("Refresh token expired.");
        }

        // Check tenant member status
        if (!storedToken.TenantMember.IsActive)
        {
            throw new AuthenticationException("Account is suspended or removed.");
        }

        // Revoke current token (rotation)
        storedToken.RevokeForRotation(now);

        // Build new claims from current member state
        var member = storedToken.TenantMember;
        var roles = member.RoleAssignments
            .Select(ra => ra.Role.Code)
            .ToList();

        var permissions = member.RoleAssignments
            .SelectMany(ra => ra.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var orgIds = member.OrganizationMemberships
            .Select(om => om.OrganizationId)
            .ToList();

        var claims = new TokenClaims(
            storedToken.UserId,
            storedToken.TenantId,
            storedToken.TenantMemberId,
            orgIds,
            roles,
            permissions);

        // Calculate new expiration (respect absolute expiration)
        var newSlidingExpires = now.AddDays(_options.RefreshTokenSlidingDays);
        var effectiveExpires = newSlidingExpires < storedToken.AbsoluteExpiresAt
            ? newSlidingExpires
            : storedToken.AbsoluteExpiresAt;

        // Generate new token pair
        var signingKey = await GetOrCreateActiveSigningKeyAsync(cancellationToken);
        var accessTokenExpires = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var accessToken = GenerateAccessToken(claims, signingKey, now, accessTokenExpires);

        var (newRefreshToken, newRefreshTokenHash) = GenerateRefreshToken();

        // Create rotated refresh token
        var newRefreshTokenEntity = RefreshToken.CreateRotated(
            tokenHash: newRefreshTokenHash,
            previousToken: storedToken,
            expiresAt: effectiveExpires);

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        return new TokenPair(
            accessToken,
            newRefreshToken,
            accessTokenExpires,
            effectiveExpires);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, string reason, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var tokenHash = ComputeTokenHash(refreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null)
        {
            return; // Token not found, nothing to revoke
        }

        // Revoke entire family for security
        await RevokeFamilyAsync(storedToken.FamilyId, reason, cancellationToken);
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, Guid tenantId, string reason, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.TenantId == tenantId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(reason, now);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BlacklistAccessTokenAsync(string accessToken, string reason, CancellationToken cancellationToken = default)
    {
        var handler = new JsonWebTokenHandler();

        try
        {
            var jwt = handler.ReadJsonWebToken(accessToken);
            var jti = jwt.GetClaim(JwtRegisteredClaimNames.Jti)?.Value;
            var exp = jwt.GetClaim(JwtRegisteredClaimNames.Exp)?.Value;
            var sub = jwt.GetClaim(JwtRegisteredClaimNames.Sub)?.Value;
            var tid = jwt.GetClaim(TenantIdClaim)?.Value;

            if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(exp))
            {
                return;
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp));
            var now = _dateTimeProvider.UtcNow;

            // Only blacklist if token hasn't expired yet
            if (expiresAt <= now)
            {
                return;
            }

            Guid? userId = Guid.TryParse(sub, out var uid) ? uid : null;
            Guid? tenantId = Guid.TryParse(tid, out var t) ? t : null;

            var blacklistEntry = TokenBlacklist.Create(
                jti: jti,
                expiresAt: expiresAt,
                reason: reason,
                revokedAt: now,
                userId: userId,
                tenantId: tenantId);

            _context.TokenBlacklist.Add(blacklistEntry);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Invalid token format, nothing to blacklist
        }
    }

    /// <summary>
    /// Gets JWKS (JSON Web Key Set) for token verification.
    /// </summary>
    public async Task<object> GetJwksAsync(CancellationToken cancellationToken = default)
    {
        var validKeys = await _context.SigningKeys
            .Where(k => k.Status == "active" || k.Status == "rotate_out")
            .ToListAsync(cancellationToken);

        var keys = validKeys.Select(k =>
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(k.PublicKeyPem);
            var parameters = ecdsa.ExportParameters(false);

            return new
            {
                kty = "EC",
                use = "sig",
                kid = k.KeyId,
                alg = k.Algorithm,
                crv = "P-256",
                x = Base64UrlEncode(parameters.Q.X!),
                y = Base64UrlEncode(parameters.Q.Y!)
            };
        }).ToList();

        return new { keys };
    }

    private string GenerateAccessToken(TokenClaims claims, SigningKey signingKey, DateTimeOffset now, DateTimeOffset expires)
    {
        // Note: ECDsa is not disposed here because ECDsaSecurityKey may hold a reference
        // and JsonWebTokenHandler.CreateToken may use it lazily. The key will be GC'd.
        var ecdsa = LoadEcdsaPrivateKey(signingKey);
        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = signingKey.KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(TenantIdClaim, claims.TenantId.ToString()),
            new(MemberIdClaim, claims.TenantMemberId.ToString())
        };

        // Add organization IDs as array
        foreach (var orgId in claims.OrganizationIds)
        {
            jwtClaims.Add(new Claim(OrganizationIdsClaim, orgId.ToString()));
        }

        // Add roles
        foreach (var role in claims.Roles)
        {
            jwtClaims.Add(new Claim(RolesClaim, role));
        }

        // Add permissions
        foreach (var permission in claims.Permissions)
        {
            jwtClaims.Add(new Claim(PermissionsClaim, permission));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(jwtClaims),
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    private static (string Token, byte[] Hash) GenerateRefreshToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncode(tokenBytes);
        var hash = ComputeTokenHash(token);
        return (token, hash);
    }

    private static byte[] ComputeTokenHash(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private async Task<SigningKey> GetOrCreateActiveSigningKeyAsync(CancellationToken cancellationToken)
    {
        var activeKey = await _context.SigningKeys
            .Where(k => k.Status == "active")
            .OrderByDescending(k => k.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeKey != null)
        {
            return activeKey;
        }

        // Create new signing key
        var now = _dateTimeProvider.UtcNow;
        var keyId = $"key-{now:yyyy-MM-dd}-{Guid.NewGuid().ToString()[..8]}";
        var expiresAt = now.AddDays(_options.SigningKeyLifetimeDays);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var privateKeyPem = ecdsa.ExportECPrivateKeyPem();

        // For MVP, store private key with simple encoding (in production, use KMS)
        var privateKeyEncrypted = Encoding.UTF8.GetBytes(privateKeyPem);

        var newKey = SigningKey.Create(
            keyId: keyId,
            publicKeyPem: publicKeyPem,
            privateKeyEncrypted: privateKeyEncrypted,
            expiresAt: expiresAt);

        newKey.Activate(now);

        _context.SigningKeys.Add(newKey);
        await _context.SaveChangesAsync(cancellationToken);

        return newKey;
    }

    private static ECDsa LoadEcdsaPrivateKey(SigningKey key)
    {
        var ecdsa = ECDsa.Create();
        var privateKeyPem = Encoding.UTF8.GetString(key.PrivateKeyEncrypted);
        ecdsa.ImportFromPem(privateKeyPem);
        return ecdsa;
    }

    private static ECDsaSecurityKey LoadEcdsaPublicKey(SigningKey key)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(key.PublicKeyPem);
        return new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId };
    }

    private async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;

        var familyTokens = await _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.Revoke(reason, now);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static TokenClaims ExtractClaims(IDictionary<string, object> claims)
    {
        var userId = Guid.Parse(claims[JwtRegisteredClaimNames.Sub].ToString()!);

        // Tenant and member IDs are optional (user-only tokens don't have them)
        var tenantId = Guid.Empty;
        var memberId = Guid.Empty;
        if (claims.TryGetValue(TenantIdClaim, out var tidValue))
        {
            tenantId = Guid.Parse(tidValue.ToString()!);
        }
        if (claims.TryGetValue(MemberIdClaim, out var midValue))
        {
            memberId = Guid.Parse(midValue.ToString()!);
        }

        var orgIds = new List<Guid>();
        if (claims.TryGetValue(OrganizationIdsClaim, out var orgValue))
        {
            if (orgValue is IEnumerable<object> orgList)
            {
                orgIds.AddRange(orgList.Select(o => Guid.Parse(o.ToString()!)));
            }
            else if (orgValue is string orgStr)
            {
                orgIds.Add(Guid.Parse(orgStr));
            }
        }

        var roles = new List<string>();
        if (claims.TryGetValue(RolesClaim, out var roleValue))
        {
            if (roleValue is IEnumerable<object> roleList)
            {
                roles.AddRange(roleList.Select(r => r.ToString()!));
            }
            else if (roleValue is string roleStr)
            {
                roles.Add(roleStr);
            }
        }

        var permissions = new List<string>();
        if (claims.TryGetValue(PermissionsClaim, out var permValue))
        {
            if (permValue is IEnumerable<object> permList)
            {
                permissions.AddRange(permList.Select(p => p.ToString()!));
            }
            else if (permValue is string permStr)
            {
                permissions.Add(permStr);
            }
        }

        return new TokenClaims(userId, tenantId, memberId, orgIds, roles, permissions);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
