---
name: security-conventions
description: Security patterns for Authra — UUID v7 IDs, ID prefixes, Argon2id password hashing, JWT ES256 signing.
version: 2.2.0
paths:
  - "src/Authra.Api/**/*"
  - "src/Authra.Application/Auth/**/*"
  - "src/Authra.Infrastructure/Services/*Password*.cs"
  - "src/Authra.Infrastructure/Services/*Token*.cs"
  - "src/Authra.Infrastructure/Services/*Jwt*.cs"
---

# Security Conventions

Security-critical patterns. Cryptographic choices, key generation, and credential handling. **Date**: 2026-01-25.

## ID Generation — UUID v7

**Decision**: UUID v7 with PostgreSQL native `uuidv7()` function.

| Entity Type | Generation Location | Method |
|-------------|---------------------|--------|
| All database entities | Database | `DEFAULT uuidv7()` |
| Application-generated (RefreshToken hash, etc.) | Application | `Medo.Uuid7` |

### Why UUID v7 over UUID v4

| Aspect | UUID v4 (random) | UUID v7 (time-ordered) |
|--------|------------------|------------------------|
| B-tree index fragmentation | ~500x more page splits | Sequential, optimal |
| Insert performance | Degrades over time | Consistent |
| Sortable by time | No | Yes |
| Index page efficiency | ~69% full | ~100% full |

### PostgreSQL 18.1 Native Support

PostgreSQL 18 added native `uuidv7()` — no extensions needed:

```sql
SELECT uuidv7();  -- 019458a0-4e28-7def-8c12-fb3a4e5d6c7a
```

### .NET 10 Caveat

.NET 10 has `Guid.CreateVersion7()` but it has **byte-ordering issues** causing index fragmentation. Use `Medo.Uuid7` instead for application-side generation.

### Implementation

**Database schema**:
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuidv7(),
    created_at TIMESTAMPTZ DEFAULT now()
);
```

**EF Core configuration**:
```csharp
modelBuilder.Entity<User>()
    .Property(u => u.Id)
    .HasDefaultValueSql("uuidv7()");
```

**Application-side (when needed)**:
```csharp
using Medo;
var id = Uuid7.NewUuid7().ToGuid();
```

### ID Prefixes (Presentation Layer)

API responses use type-prefixed IDs for developer experience:

| Prefix | Resource |
|--------|----------|
| `usr_` | User |
| `tnt_` | Tenant |
| `mbr_` | TenantMember |
| `org_` | Organization |
| `rol_` | Role |
| `prm_` | Permission |
| `inv_` | Invite |
| `req_` | Request ID (tracing) |

**Implementation**: Prefixes are presentation-layer only. Database stores raw UUID v7. Encoding/decoding happens at API boundary.

**Trade-offs**: 16 bytes vs 8 bytes for BIGSERIAL (acceptable). Prefix encoding adds ~1μs per ID (negligible).

## Password Hashing — Argon2id

**Decision**: Argon2id with abstracted `IPasswordHasher` interface for algorithm flexibility.

### Strategy

| Aspect | Choice |
|--------|--------|
| Algorithm | Argon2id |
| Library | `Konscious.Security.Cryptography.Argon2` 1.3.1 |
| Parameters | m=47104 (46 MiB), t=1, p=1 |
| Salt | 16 bytes (128-bit) |
| Hash | 32 bytes (256-bit) |
| Hash Format | PHC string (self-describing) |

### Abstraction for Algorithm Flexibility

```csharp
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string password, string hashedPassword);
    bool NeedsRehash(string hashedPassword);  // For algorithm migration
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded  // Algorithm or params changed
}
```

### PHC String Format

```
$argon2id$v=19$m=47104,t=1,p=1$<base64-salt>$<base64-hash>
```

Self-describing format enables:
- Algorithm identification during verification
- Gradual parameter strengthening
- Re-hash on login when parameters change
- Multiple algorithms coexisting during migration

**Trade-offs**: ~150-200ms per hash operation (acceptable for auth, not bulk). More complex than bcrypt's single function call (mitigated by abstraction).

**Deferred to v1.1**: Pepper storage in secrets vault/HSM. Adaptive parameter tuning based on server load.

## JWT Access Tokens — ES256

See `.claude/rules/architecture-decisions.md` § Session and Token Management and the `security-practices` skill for token issuance, refresh rotation, reuse detection, and per-claim details. Keys live in the `SigningKey` table with rotation support; access tokens are 15 min ES256, opaque refresh tokens are 30-day sliding with 90-day absolute cap.
