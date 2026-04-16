---
name: data-model
description: Authra entity and persistence patterns — 20-entity MVP schema, EF Core UUID v7 defaults, Row-Level Security, tenant-scoped tables.
version: 2.2.0
paths:
  - "src/Authra.Infrastructure/Persistence/**/*"
  - "src/Authra.Domain/Entities/**/*"
  - "src/Authra.Domain/ValueObjects/**/*"
---

# Data Model

Entity patterns, EF Core configuration, and PostgreSQL persistence. Full 20-entity specification lives in [CLAUDE-DATA-MODEL.md](../../CLAUDE-DATA-MODEL.md).

## MVP Entity Groups

| Layer | Entities |
|-------|----------|
| Identity (Global) | `User`, `UserIdentifier`, `PasswordAuth`, `PasswordResetToken`, `ExternalAuth` |
| Tenant (RLS) | `Tenant`, `TenantMember`, `Invite`, `Organization`, `OrganizationMember` |
| RBAC | `Permission`, `Role`, `RolePermission`, `RoleOrganization`, `TenantMemberRole` |
| Audit | `OwnershipTransfer` |
| Session/Token | `SigningKey`, `RefreshToken`, `TokenBlacklist` |

- **20 entities** total
- **19 system permissions** with colon notation
- **v1.1 ready**: Schema includes columns for custom permissions and role restrictions (disabled by default via `Tenant.AllowCustomPermissions` and `Tenant.AllowOrgRestrictions`)

## Primary Keys — UUID v7

All entities use UUID v7 primary keys with PostgreSQL `uuidv7()` as the column default.

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

**Application-side generation** (e.g. when pre-computing IDs before insert):
```csharp
using Medo;
var id = Uuid7.NewUuid7().ToGuid();  // Do NOT use Guid.CreateVersion7() — byte-ordering bug
```

See `.claude/rules/security-conventions.md` § ID Generation for full rationale.

## Row-Level Security (RLS)

PostgreSQL RLS implemented at database level for every tenant-scoped table:

- All tenant-scoped tables have a `TenantId` column
- Each table has an RLS policy that filters by `current_setting('app.current_tenant')`
- Tenant context is set via `SET app.current_tenant = '{tenant_id}'` at connection acquisition
- EF Core interceptor injects the tenant context before every query

Global entities (`User`, `UserIdentifier`, `PasswordAuth`, `SigningKey`, `TokenBlacklist`, `RefreshToken`, the `Permission` catalog) are NOT tenant-scoped and do not carry `TenantId`.

## Deferred to v1.1

- Tenant-defined custom permissions (schema column exists, feature flag gated)
- Role-to-organization restrictions (schema column exists, feature flag gated)
- Two-phase ownership transfer workflow
- External auth providers (Google/Apple/Microsoft)

Scaffold new entities with `/scaffold-entity <EntityName>`. See the `database-conventions` skill for entity patterns, configuration conventions, and migration guidance.
