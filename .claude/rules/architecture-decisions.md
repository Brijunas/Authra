---
name: architecture-decisions
description: Core architectural decisions for the Authra identity system — identity model, database, permission naming, data model summary, session/token strategy, and REST API summary.
version: 2.2.0
---

# Architecture Decisions

Strategic decisions that define Authra. Each section captures the decision, the rationale, and accepted trade-offs. For full research and rejected alternatives see `CLAUDE-RESEARCHED.md`.

## Identity Model — Proposal B (Separated Identifiers and Credentials)

**Date**: 2026-01-15

```
User(UserId)
    │
    ├── UserIdentifier(UserId, Type, ValueNormalized)
    │       UNIQUE(Type, ValueNormalized)
    │
    ├── PasswordAuth(UserId, PasswordHash, Algorithm, Params)
    │
    └── ExternalAuth(UserId, Provider, SubjectId, ProviderData)
            UNIQUE(Provider, SubjectId)
```

**Rationale**:
1. **Industry alignment**: Auth0, Okta, Keycloak, AWS Cognito, ASP.NET Core Identity all use this pattern
2. **Separation of concerns**: Identity → Identifiers → Credentials
3. **Extensibility**: Adding new auth providers requires configuration, not schema changes
4. **Security**: Credentials isolated from PII with different blast radius
5. **Flexibility**: Multiple identifiers (username + email + phone) without denormalization
6. **Algorithm migration**: Explicit params enable gradual credential upgrades

**Trade-offs**: Login requires 2 queries (resolve identifier → verify credential). Slightly more complex initial implementation.

## Database — PostgreSQL

**Date**: 2026-01-15

Industry precedent: Keycloak, FusionAuth, SuperTokens, Authelia, Authentik all use PostgreSQL. Auth0 migrated from MongoDB to PostgreSQL for new services.

**Rationale**:
1. **ACID transactions**: Registration (User + UserIdentifier + PasswordAuth) must be atomic
2. **Unique constraints**: `UNIQUE(Type, ValueNormalized)` enforced atomically
3. **Row-Level Security**: Multi-tenant isolation at database level
4. **Schema alignment**: Normalized relational schema maps directly
5. **OLTP performance**: 3-15x faster for authentication workloads
6. **Ecosystem**: Most identity frameworks assume relational databases

**Recommended infrastructure**: AWS RDS PostgreSQL 15+, Multi-AZ, read replicas, PgBouncer, Row-Level Security.

**Trade-offs**: Horizontal scaling requires additional tooling (Citus, read replicas) vs MongoDB's native sharding.

## Permission Naming — Colon notation with `action:resource`

**Date**: 2026-01-25

Format: `{action}:{resource}[.{subresource}]`

Examples: `accounts:read`, `roles:assign`, `organizations:members.write`

**Why colon**:
1. **Identity domain standard**: Auth0, Keycloak use colons
2. **AWS precedent**: IAM uses colons (`s3:GetObject`, `iam:CreateUser`)
3. **OAuth community**: `action:resource` with colons is de facto standard
4. **Clear semantics**: Unambiguous visual separation
5. **Hybrid support**: Dots work for sub-resources (`organizations:members.write`)

**Trade-offs**: Colons require URL encoding (`%3A`) in query strings (mitigated: permissions typically live in JWT claims/headers).

## MVP Data Model — Future-proof schema

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-DATA-MODEL.md](../../CLAUDE-DATA-MODEL.md) and `.claude/rules/data-model.md`.

- **20 entities** covering identity, tenants, organizations, RBAC, session management
- **19 system permissions** with colon notation
- **v1.1 ready**: Schema includes columns for custom permissions and role restrictions (disabled by default)
- **Feature flags**: `Tenant.AllowCustomPermissions`, `Tenant.AllowOrgRestrictions`

**Entity groups**:

| Layer | Entities |
|-------|----------|
| Identity (Global) | `User`, `UserIdentifier`, `PasswordAuth`, `PasswordResetToken`, `ExternalAuth` |
| Tenant (RLS) | `Tenant`, `TenantMember`, `Invite`, `Organization`, `OrganizationMember` |
| RBAC | `Permission`, `Role`, `RolePermission`, `RoleOrganization`, `TenantMemberRole` |
| Audit | `OwnershipTransfer` |
| Session/Token | `SigningKey`, `RefreshToken`, `TokenBlacklist` |

**Deferred to v1.1**: Tenant-defined custom permissions, role-to-organization restrictions, two-phase ownership transfer, external auth providers (Google/Apple/Microsoft).

## Session and Token Management — JWT ES256 + opaque refresh

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-SESSION-TOKENS.md](../../CLAUDE-SESSION-TOKENS.md) and `.claude/rules/security-conventions.md`.

- **JWT access tokens**: 15 min lifetime, ES256 signing, tenant-scoped claims
- **Opaque refresh tokens**: 30 days sliding, 90 days absolute, rotation with reuse detection
- **MVP PostgreSQL-only**: `TokenBlacklist` table for revocation (no Redis initially)
- **Multi-tenant**: Tenant context in all tokens; seamless switching

| Token Type | Format | Lifetime | Storage |
|------------|--------|----------|---------|
| Access Token | JWT (ES256) | 15 min | Client memory |
| Refresh Token | Opaque | 30 days sliding | HttpOnly cookie + PostgreSQL |

**Access token claims**: `sub` (User ID), `tid` (Tenant ID), `mid` (TenantMember ID), `oid` (Organization IDs), `roles`, `permissions`.

**Deferred to v1.1**: Redis session cache, BFF pattern, phantom tokens/token exchange, per-tenant token settings, concurrent session limits, `SecurityEvent` table, `RateLimitCounter` table.

## REST API Design — RESTful with URL path versioning

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-API.md](../../CLAUDE-API.md) and `.claude/rules/api-design.md`.

- **39 endpoints** across auth, users, tenants, organizations, roles, permissions
- **URL versioning**: All endpoints under `/v1` prefix
- **Cursor-based pagination** for all list endpoints
- **Consistent error format**: Structured errors with codes and request IDs (RFC 9457)

**Endpoint categories**: Authentication (8), Current User (3), Tenants (5), Tenant Members (6), Organizations (5), Organization Members (3), Roles (5), Role Assignments (3), Permissions (1).

**Deferred to v1.1**: Session management endpoints (`/auth/sessions`), tenant suspension/deletion, audit log endpoints, external OAuth providers, API keys, webhooks.
