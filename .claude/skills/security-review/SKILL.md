---
name: security-review
description: "Comprehensive security review of a feature, endpoint, or file — covering OWASP Top 10, auth flows, cryptography, and data protection."
when_to_use: "When the user runs `/security-review` or asks for a security-focused audit of specific Authra code."
version: 2.2.0
---

# Security Review

Performs a comprehensive security review identifying vulnerabilities and ensuring security best practices.

## Usage

```
/security-review <path> [--focus <area>]
```

## Arguments

- `<path>`: File, folder, or feature to review (e.g., `src/Authra.Api/Endpoints/Auth/`)
- `--focus`: Optional focus area (auth, validation, data, crypto)

## What This Skill Does

### Step 1: Gather Context

1. Read the target code files
2. Review related convention skills (security-practices)
3. Understand the security requirements from CLAUDE.md
4. Identify security-sensitive operations

### Step 2: Authentication Review

Check JWT and token handling:

- [ ] JWT tokens use ES256 (ECDSA P-256) signing
- [ ] Access token lifetime is 15 minutes
- [ ] Refresh tokens are opaque (not JWT)
- [ ] Refresh tokens stored as SHA-256 hash
- [ ] Token rotation implemented
- [ ] Reuse detection enabled
- [ ] No algorithm confusion vulnerability

**Patterns to check:**

```csharp
// BAD: Algorithm not validated
var handler = new JwtSecurityTokenHandler();
handler.ValidateToken(token, new TokenValidationParameters
{
    ValidateIssuerSigningKey = false  // VULNERABLE
});

// GOOD: Explicit algorithm
new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidAlgorithms = ["ES256"]
}
```

### Step 3: Authorization Review

Check permission enforcement:

- [ ] All endpoints have authorization
- [ ] Permission checks use `RequirePermissionAttribute`
- [ ] No authorization bypass paths
- [ ] Row-Level Security enabled for tenant data
- [ ] Tenant context validated

**Patterns to check:**

```csharp
// BAD: Missing authorization
group.MapGet("/", GetAllAsync); // No RequireAuthorization!

// GOOD: Proper authorization
group.MapGet("/", GetAllAsync)
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("accounts:read"));
```

### Step 4: Password & Credential Review

Check password handling:

- [ ] Argon2id algorithm used
- [ ] Correct parameters (m=47104, t=1, p=1)
- [ ] PHC string format
- [ ] Constant-time comparison
- [ ] Passwords never logged
- [ ] Passwords never in responses

**Patterns to check:**

```csharp
// BAD: Timing attack vulnerable
if (password != storedPassword) return false;

// GOOD: Constant-time comparison
var result = CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(computed),
    Encoding.UTF8.GetBytes(stored));
```

### Step 5: Input Validation Review

Check input handling:

- [ ] All endpoints use FluentValidation
- [ ] Email normalized before comparison
- [ ] No SQL injection (parameterized queries)
- [ ] No command injection
- [ ] Request size limits configured
- [ ] Content-Type validated

**Patterns to check:**

```csharp
// BAD: SQL injection
var sql = $"SELECT * FROM users WHERE email = '{email}'";

// GOOD: Parameterized via EF Core
var user = await context.Users
    .FirstOrDefaultAsync(u => u.Email == email);
```

### Step 6: Data Protection Review

Check sensitive data handling:

- [ ] No passwords in logs
- [ ] No tokens in logs
- [ ] No PII in error responses
- [ ] Sensitive fields excluded from DTOs
- [ ] Connection strings in secrets management

**Patterns to check:**

```csharp
// BAD: Logs sensitive data
logger.LogInformation("Login: {Email} {Password}", email, password);

// GOOD: Only identifiers
logger.LogInformation("Login attempt for {Email}", email);
```

### Step 7: Cryptographic Review

Check cryptography:

- [ ] `RandomNumberGenerator.GetBytes()` for secure random
- [ ] No `System.Random` for security
- [ ] ECDSA keys properly generated
- [ ] Secrets stored hashed
- [ ] Key rotation mechanism exists

**Patterns to check:**

```csharp
// BAD: Predictable random
var random = new Random();
var token = random.Next().ToString();

// GOOD: Cryptographically secure
var bytes = RandomNumberGenerator.GetBytes(32);
var token = Convert.ToBase64String(bytes);
```

### Step 8: Security Headers Review

Check HTTP security:

- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-Frame-Options: DENY`
- [ ] `X-XSS-Protection: 1; mode=block`
- [ ] `Referrer-Policy: strict-origin-when-cross-origin`
- [ ] `Content-Security-Policy` configured
- [ ] HTTPS enforced
- [ ] CORS properly configured

### Step 9: Generate Report

Output a structured security report:

```markdown
# Security Review Report

**Target**: {path}
**Date**: {date}
**Reviewer**: security-analyzer agent

## Summary
{Overall security assessment}

## Critical Issues
{Must fix before production}

## High Priority
{Should fix soon}

## Medium Priority
{Recommended improvements}

## Low Priority
{Nice to have}

## Passed Checks
{What's correctly implemented}
```

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `security-practices` | JWT, tokens, Argon2id, permissions |
| `api-conventions` | Validation patterns |
| `database-conventions` | RLS enforcement |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `security-analyzer` | Primary security analysis |
| `code-reviewer` | Additional code quality |

## Context7 Usage

Query security library documentation when reviewing:
- JWT library patterns
- Cryptography best practices
- ASP.NET Core security features

## Example

```bash
/security-review src/Authra.Api/Endpoints/Auth/
```

**Outputs:**

```markdown
# Security Review Report

**Target**: src/Authra.Api/Endpoints/Auth/
**Date**: 2026-01-25

## Summary
Authentication endpoints follow most security best practices.
Found 1 critical issue and 2 medium-priority improvements.

## Critical Issues

### 1. Missing Rate Limiting on Login Endpoint
**File**: AuthEndpoints.cs:45
**Issue**: Login endpoint has no rate limiting, enabling brute force attacks
**Fix**: Add rate limiting policy

```csharp
group.MapPost("/login", LoginAsync)
    .RequireRateLimiting("auth");
```

## High Priority
None

## Medium Priority

### 1. Token Reuse Detection Logging
**File**: TokenService.cs:123
**Issue**: Token reuse detection doesn't log the security event
**Fix**: Add security event logging

### 2. Password Reset Token Entropy
**File**: PasswordResetService.cs:67
**Issue**: Using 16 bytes for reset token, recommend 32 bytes
**Fix**: Increase to 32 bytes

## Passed Checks
- [x] JWT uses ES256 signing
- [x] Refresh tokens hashed with SHA-256
- [x] Argon2id with correct parameters
- [x] No passwords in logs
- [x] FluentValidation on all endpoints
- [x] Parameterized queries via EF Core
```

## Checklist

Security review should verify:

- [ ] Authentication implementation secure
- [ ] Authorization properly enforced
- [ ] Password handling follows best practices
- [ ] Input validation comprehensive
- [ ] Sensitive data protected
- [ ] Cryptography correctly implemented
- [ ] Security headers configured
- [ ] No common vulnerabilities (OWASP Top 10)
