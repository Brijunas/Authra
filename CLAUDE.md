# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Purpose

This is a design and architecture workspace for planning a multi-tenant identity and authentication system. It contains architectural proposals, design decisions, and custom Claude Code agents for architecture review and research.

## Available Custom Agents

Nine specialized agents are configured in `.claude/agents/` and are automatically available via the Task tool. **Proactively use these agents when the task matches their expertise—do not wait for explicit user request.**

### Architecture & Research

| Agent | Purpose | Model |
|-------|---------|-------|
| `architect-reviewer` | System design validation, pattern evaluation, scalability analysis | opus |
| `research-analyst` | Information gathering, synthesis, web search | opus |
| `api-designer` | REST/GraphQL API design, OpenAPI documentation | opus |

### Development

| Agent | Purpose | Model |
|-------|---------|-------|
| `dotnet-developer` | Feature implementation, endpoints, services, DTOs | sonnet |
| `ef-core-specialist` | EF Core configuration, migrations, RLS, query optimization | opus |
| `test-engineer` | Unit and integration tests, Testcontainers, fixtures | sonnet |

### Review

| Agent | Purpose | Model |
|-------|---------|-------|
| `code-reviewer` | Code quality, conventions, architecture compliance | opus |
| `security-reviewer` | Security audit, auth/authz, cryptography, vulnerabilities | opus |
| `performance-reviewer` | N+1 queries, indexes, caching, async patterns | opus |

## Available Skills

Skills are organized in `.claude/skills/` with two categories:

### Convention Skills (Auto-loaded)

Convention skills in `.claude/skills/conventions/` provide coding standards and patterns:

| Skill | Purpose |
|-------|---------|
| `authra-data-model` | Entity patterns, UUID v7, RLS, EF Core migrations |
| `dotnet-conventions` | C# 14/.NET 10 coding standards, naming, patterns |
| `authra-security` | JWT tokens, refresh tokens, Argon2id, permissions |
| `clean-architecture` | Layer boundaries, feature folders, DI patterns |
| `testing-patterns` | xUnit v3, Testcontainers, Respawn, assertions |
| `api-conventions` | REST patterns, validation, error handling, pagination |

### Workflow Skills (Slash Commands)

Workflow skills in `.claude/skills/workflows/` provide automation commands:

| Command | Usage | Purpose |
|---------|-------|---------|
| `/scaffold-entity` | `/scaffold-entity <EntityName>` | Create entity with Domain, EF config, migration |
| `/scaffold-feature` | `/scaffold-feature <FeatureName>` | Create complete feature across all layers |
| `/add-endpoint` | `/add-endpoint "<METHOD> /v1/{path}"` | Add endpoint to existing feature |
| `/test-feature` | `/test-feature <FeatureName>` | Generate comprehensive test suite |
| `/security-review` | `/security-review <path>` | Security audit of code |
| `/review` | `/review <path>` | General code review |
| `/performance-check` | `/performance-check <path>` | Performance analysis |

### Typical Development Flow

```bash
# 1. Scaffold entity from data model
/scaffold-entity Organization

# 2. Scaffold feature with CRUD endpoints
/scaffold-feature Organizations --entity Organization

# 3. Add custom endpoints
/add-endpoint "POST /v1/organizations/{id}/members" --permission "organizations:members.write"

# 4. Generate tests
/test-feature Organizations

# 5. Review before commit
/review src/Authra.Application/Organizations/
/security-review src/Authra.Api/Endpoints/Organizations/
/performance-check src/Authra.Infrastructure/Services/OrganizationService.cs
```

## Tool Usage

- **Context7 MCP**: Always use Context7 MCP tools (`resolve-library-id` and `query-docs`) when working with library/API documentation, code generation, setup, or configuration steps. Use proactively without waiting for explicit user request.

### MCP Dependencies

The following agents require MCP tools to function fully:

| Agent | MCP Tools Required | Purpose |
|-------|-------------------|---------|
| `research-analyst` | WebFetch, WebSearch | Web research and information gathering |
| `ef-core-specialist` | Context7 | EF Core and Npgsql documentation |
| `dotnet-developer` | Context7 | ASP.NET Core and FluentValidation docs |
| `test-engineer` | Context7 | xUnit, Testcontainers documentation |
| `security-reviewer` | Context7 | Security library documentation |
| `performance-reviewer` | Context7 | Performance optimization patterns |
| `code-reviewer` | Context7 | Library API verification |

**Context7 Libraries Used**:
- `/efcore/efcore` - EF Core 10 patterns
- `/npgsql/npgsql` - PostgreSQL provider
- `/dotnet/aspnetcore` - ASP.NET Core 10
- `/fluentvalidation/fluentvalidation` - Request validation
- `/xunit/xunit` - xUnit v3 testing
- `/testcontainers/testcontainers-dotnet` - Testcontainers

## Current Design Context

The workspace contains proposals for a multi-tenant system with:
- Global identity and authentication (username + password initially)
- Tenant onboarding (create or join tenants)
- Tenant administration (accounts, organizations, roles, permissions)
- Organizations within tenants; accounts can access multiple organizations
- Future third-party auth (Google/Apple/Microsoft)

## Architectural Decision: Identity Model

**Decision**: Proposal B (Separated Identifiers and Credentials)

**Date**: 2026-01-15

### Decided Schema

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

### Rationale

1. **Industry alignment**: Matches patterns used by Auth0, Okta, Keycloak, AWS Cognito, and ASP.NET Core Identity
2. **Separation of concerns**: Identity (User) → Identifiers (UserIdentifier) → Credentials (PasswordAuth/ExternalAuth)
3. **Extensibility**: Adding new auth providers requires configuration, not schema changes
4. **Security**: Credentials isolated from PII with different blast radius
5. **Flexibility**: Users can have multiple identifiers (username + email + phone) without denormalization
6. **Algorithm migration**: Explicit params enable gradual credential algorithm upgrades

### Trade-offs Accepted

- Login requires 2 queries (resolve identifier → verify credential) vs 1 query in Proposal A
- Slightly more complex initial implementation

### Rejected Alternative (Proposal A)

- PasswordAuth storing identifiers alongside credentials violated single responsibility
- Separate table per provider would cause schema explosion
- Scored 2.4/5 vs Proposal B's 4.7/5 in architecture review

## Architectural Decision: Database

**Decision**: PostgreSQL (Relational Database)

**Date**: 2026-01-15

### Industry Precedent

| Identity Provider | Database |
|-------------------|----------|
| Auth0 | MongoDB (legacy) → PostgreSQL (new services) |
| Keycloak | PostgreSQL (phasing out MySQL/Oracle) |
| FusionAuth | PostgreSQL |
| SuperTokens | PostgreSQL |
| Authelia | PostgreSQL + Redis |
| Authentik | PostgreSQL only |

### Rationale

1. **ACID transactions**: User registration (User + UserIdentifier + PasswordAuth) must be atomic — PostgreSQL native; MongoDB added in v4.0 with limitations
2. **Unique constraints**: `UNIQUE(Type, ValueNormalized)` enforced atomically; MongoDB has race condition risks with array indexes
3. **Row-Level Security**: Multi-tenant isolation at database level eliminates application-layer data leakage bugs
4. **Schema alignment**: Normalized relational schema maps directly to PostgreSQL
5. **OLTP performance**: 3-15x faster for authentication workloads in benchmarks
6. **Ecosystem**: Keycloak, Ory, and most identity frameworks assume relational databases

### Recommended Infrastructure

```
AWS RDS PostgreSQL 15+
├── Multi-AZ deployment (high availability)
├── Read replicas (authentication query scaling)
├── PgBouncer (connection pooling for 10K+ connections)
└── Row-Level Security (tenant isolation)
```

### Trade-offs Accepted

- Horizontal scaling requires additional tooling (Citus extension, read replicas) vs MongoDB's native sharding
- More upfront schema design (mitigated: schema already decided)

### Rejected Alternative (MongoDB)

- Unique constraints on arrays have race condition risks
- Multi-document transactions added in v4.0, less mature than PostgreSQL
- No native Row-Level Security for multi-tenant isolation
- Scored 6.2/10 vs PostgreSQL's 9.2/10 in architecture review

## Architectural Decision: Permission Naming Convention

**Decision**: Colon notation (`:`) with `action:resource` pattern

**Date**: 2026-01-25

### Format

```
{action}:{resource}[.{subresource}]
```

Examples: `accounts:read`, `roles:assign`, `organizations:members.write`

### Industry Research Summary

| System | Separator | Domain |
|--------|-----------|--------|
| Auth0 | Colon (`:`) | Identity provider |
| AWS IAM | Colon (`:`) | Cloud infrastructure |
| GitHub OAuth | Colon (`:`) | OAuth scopes |
| Keycloak | Colon (`:`) | Identity provider |
| Google Cloud IAM | Dot (`.`) | Cloud infrastructure |
| Microsoft Graph | Dot (`.`) | API permissions |

### Why Colon (`:`)

1. **Identity domain standard**: Auth0 (industry leader in identity-as-a-service) uses colon notation exclusively
2. **AWS precedent**: AWS IAM, the most widely deployed authorization system, uses colons (`s3:GetObject`, `iam:CreateUser`)
3. **OAuth community preference**: The `action:resource` pattern with colons is the de facto standard in OAuth implementations
4. **Clear semantics**: Unambiguous visual separation between action and resource
5. **Hybrid support**: Dots work naturally for sub-resources (`organizations:members.write`)

### Trade-offs Accepted

- Colons require URL encoding (`%3A`) in query strings (mitigated: permissions typically in JWT claims/headers)
- Different from cloud infrastructure IAM like GCP (uses dots)

## Architectural Decision: MVP Data Model

**Decision**: Future-proof schema with system permissions, tenant-defined roles

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-DATA-MODEL.md](./CLAUDE-DATA-MODEL.md)

### Summary

- **20 entities** covering identity, tenants, organizations, RBAC, and session management
- **19 system permissions** with colon notation
- **v1.1 ready**: Schema includes columns for custom permissions and role restrictions (disabled by default)
- **Feature flags**: Enable v1.1 features via `Tenant.AllowCustomPermissions` and `Tenant.AllowOrgRestrictions`

### Key Entities

| Layer | Entities |
|-------|----------|
| Identity (Global) | `User`, `UserIdentifier`, `PasswordAuth`, `PasswordResetToken`, `ExternalAuth` |
| Tenant (RLS) | `Tenant`, `TenantMember`, `Invite`, `Organization`, `OrganizationMember` |
| RBAC | `Permission`, `Role`, `RolePermission`, `RoleOrganization`, `TenantMemberRole` |
| Audit | `OwnershipTransfer` |
| Session/Token | `SigningKey`, `RefreshToken`, `TokenBlacklist` |

### Deferred to v1.1

- Tenant-defined custom permissions
- Role-to-organization restrictions
- Two-phase ownership transfer workflow
- External auth providers (Google/Apple/Microsoft)

## Architectural Decision: Session and Token Management

**Decision**: JWT access tokens (ES256) with rotating opaque refresh tokens

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-SESSION-TOKENS.md](./CLAUDE-SESSION-TOKENS.md)

### Summary

- **JWT access tokens**: 15 min lifetime, ES256 signing, tenant-scoped claims
- **Opaque refresh tokens**: 30 days sliding, 90 days absolute, rotation with reuse detection
- **MVP PostgreSQL-only**: TokenBlacklist table for revocation (no Redis initially)
- **Multi-tenant**: Tenant context in all tokens; seamless tenant switching

### Token Strategy

| Token Type | Format | Lifetime | Storage |
|------------|--------|----------|---------|
| Access Token | JWT (ES256) | 15 min | Client memory |
| Refresh Token | Opaque | 30 days sliding | HttpOnly cookie + PostgreSQL |

### Key Claims in Access Token

| Claim | Description |
|-------|-------------|
| `sub` | User ID (global identity) |
| `tid` | Current Tenant ID |
| `mid` | TenantMember ID |
| `oid` | Organization IDs accessible |
| `roles` | Role codes in current tenant |
| `permissions` | Flattened permission codes |

### New Entities

| Entity | Purpose |
|--------|---------|
| `SigningKey` | JWT key rotation tracking |
| `RefreshToken` | Opaque token storage with family tracking |
| `TokenBlacklist` | JWT revocation (MVP, replaced by Redis v1.1) |

### Deferred to v1.1

- Redis session cache
- BFF pattern
- Phantom tokens / token exchange
- Per-tenant token settings
- Concurrent session limits
- SecurityEvent table (audit logging)
- RateLimitCounter table (use application-level for MVP)

## Architectural Decision: REST API Design

**Decision**: RESTful API with URL path versioning

**Date**: 2026-01-25

**Full specification**: See [CLAUDE-API.md](./CLAUDE-API.md)

### Summary

- **39 endpoints** covering authentication, users, tenants, organizations, roles, and permissions
- **URL versioning**: All endpoints under `/v1` prefix
- **Cursor-based pagination**: For all list endpoints
- **Consistent error format**: Structured error responses with codes and request IDs

### Endpoint Categories

| Category | Count | Examples |
|----------|-------|----------|
| Authentication | 8 | login, register, refresh, logout, password reset |
| Current User | 3 | profile, tenant list |
| Tenants | 5 | CRUD, ownership transfer |
| Tenant Members | 6 | invite, list, update, remove |
| Organizations | 5 | CRUD within tenant |
| Organization Members | 3 | add, list, remove |
| Roles | 5 | CRUD within tenant |
| Role Assignments | 3 | assign, list, unassign |
| Permissions | 1 | list system permissions |

### Deferred to v1.1

- Session management endpoints (`/auth/sessions`)
- Tenant suspension/deletion
- Audit log endpoints
- External OAuth providers
- API keys
- Webhooks

## Architectural Decision: ID Generation

**Decision**: UUID v7 with PostgreSQL native `uuidv7()` function

**Date**: 2026-01-25

### Strategy

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

PostgreSQL 18 added native `uuidv7()` function - no extensions needed:

```sql
SELECT uuidv7();  -- 019458a0-4e28-7def-8c12-fb3a4e5d6c7a
```

### .NET 10 Caveat

.NET 10 has `Guid.CreateVersion7()` but it has **byte-ordering issues** causing index fragmentation. Use `Medo.Uuid7` instead for application-side generation.

### Implementation

**Database schema:**
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuidv7(),
    created_at TIMESTAMPTZ DEFAULT now()
);
```

**EF Core configuration:**
```csharp
modelBuilder.Entity<User>()
    .Property(u => u.Id)
    .HasDefaultValueSql("uuidv7()");
```

**Application-side (when needed):**
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

### Trade-offs Accepted

- 16 bytes vs 8 bytes for BIGSERIAL (acceptable for benefits)
- Prefix encoding adds ~1μs per ID (negligible)

## Architectural Decision: Password Hashing

**Decision**: Argon2id with abstracted `IPasswordHasher` interface for algorithm flexibility

**Date**: 2026-01-25

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

### Trade-offs Accepted

- ~150-200ms per hash operation (acceptable for auth, not bulk)
- More complex than bcrypt's single function call (mitigated by abstraction)

### Deferred to v1.1

- Pepper storage in secrets vault/HSM
- Adaptive parameter tuning based on server load

## Architectural Decision: Technology Stack

**Decision**: C# 14 / .NET 10 with PostgreSQL 18.1, Docker deployment

**Date**: 2026-01-25

### Core Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Language | C# | 14 |
| Framework | ASP.NET Core Minimal APIs | 10.0 |
| Runtime | .NET | 10.0 |
| ORM | Entity Framework Core | 10.0 |
| Database | PostgreSQL | 18.1 |
| Container | Docker (Ubuntu Chiseled) | Latest |

### NuGet Packages (MVP)

#### Database Access

| Package | Version | Purpose |
|---------|---------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 | EF Core 10 PostgreSQL provider |
| `Npgsql` | 10.0.0 | PostgreSQL driver |

#### JWT & Cryptography

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.IdentityModel.JsonWebTokens` | 8.15.0 | JWT creation/validation (ES256) |
| `Konscious.Security.Cryptography.Argon2` | 1.3.1 | Argon2id password hashing |
| `Medo.Uuid7` | 3.2.0 | UUID v7 generation (application-side) |

#### API & Validation

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | OpenAPI generation |
| `Scalar.AspNetCore` | Latest | API documentation UI (replaced Swashbuckle) |
| `FluentValidation` | 12.1.1 | Request validation |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | DI integration |

#### Observability

| Package | Version | Purpose |
|---------|---------|---------|
| `Serilog.AspNetCore` | 10.0.0 | Structured logging |
| `Serilog.Sinks.Console` | Latest | Console sink |
| `AspNetCore.HealthChecks.NpgSql` | 9.0.0 | PostgreSQL health check |

#### Testing

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit.v3` | 3.2.2 | xUnit v3 test framework |
| `xunit.runner.visualstudio` | 3.2.2 | VS integration |
| `Testcontainers.PostgreSql` | 4.10.0 | Real PostgreSQL in Docker for tests |
| `Testcontainers.XunitV3` | 4.10.0 | xUnit v3 integration |
| `Respawn` | 7.0.0 | Fast database reset between tests |
| `AwesomeAssertions` | 9.3.0 | Fluent assertions (Apache-2.0, FluentAssertions fork) |
| `NSubstitute` | 5.3.0 | Mocking framework |
| `Bogus` | 35.6.5 | Fake data generation |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.0 | WebApplicationFactory for API tests |

### Built-in .NET 10 Features (No packages needed)

| Feature | Usage |
|---------|-------|
| Rate Limiting | `AddRateLimiter()` middleware |
| CORS | `AddCors()` |
| Response Compression | `AddResponseCompression()` |
| Secure Random | `RandomNumberGenerator.GetBytes()` |
| ECDSA Keys | `ECDsa.Create(ECCurve.NamedCurves.nistP256)` |

### Docker Configuration

.NET 10 uses Ubuntu 24.04 as default (Debian no longer provided).

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Authra.Api/*.csproj", "src/Authra.Api/"]
RUN dotnet restore
COPY . .
RUN dotnet publish "src/Authra.Api/Authra.Api.csproj" -c Release -o /app/publish

# Runtime: Ubuntu Chiseled (minimal attack surface)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
USER app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Authra.Api.dll"]
```

**Chiseled image features:**
- No shell, no package manager (attackers cannot execute commands)
- Non-root user by default (`app` user)
- ~110MB image size (vs ~220MB standard)
- Minimal attack surface

### Testing Strategy

**Testcontainers**: Spins up real PostgreSQL 18.1 in Docker during tests - no mocking database behavior, catches real SQL issues.

**Respawn**: Intelligently deletes data between tests while respecting foreign keys (~10-50ms per reset vs seconds for schema recreation).

**Assembly Fixture pattern** (xUnit v3): Single PostgreSQL container shared across all tests in assembly, reset with Respawn after each test.

```csharp
[assembly: AssemblyFixture(typeof(DatabaseFixture))]

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

### Row-Level Security (RLS)

PostgreSQL RLS implemented at database level:
- Set tenant context via `SET app.current_tenant = '{tenant_id}'` at connection acquisition
- Use EF Core interceptors to inject tenant context
- All tenant-scoped tables have `TenantId` column with RLS policy

### Deferred to v1.1

| Component | Package |
|-----------|---------|
| Redis Cache | `Microsoft.Extensions.Caching.StackExchangeRedis` |
| PostgreSQL Logging | `Serilog.Sinks.Postgresql.Alternative` |
| Metrics | `prometheus-net.AspNetCore` |
| Distributed Tracing | `OpenTelemetry.Instrumentation.AspNetCore` |
| Email Service | SendGrid SDK or MailKit |
| Native AOT | `PublishAot=true` with `runtime-deps:10.0-noble-chiseled` |

## Architectural Decision: Project Structure

**Decision**: Pure Clean Architecture with minimal DDD, feature folders in Application layer

**Date**: 2026-01-25

### Structure

```
src/
├── Authra.Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   └── Exceptions/
│
├── Authra.Application/
│   ├── DependencyInjection.cs
│   ├── Auth/
│   │   ├── IAuthService.cs
│   │   ├── DTOs/
│   │   └── Validators/
│   ├── Tenants/
│   │   ├── ITenantService.cs
│   │   └── DTOs/
│   ├── Organizations/
│   ├── Roles/
│   └── Common/
│       └── Interfaces/
│           └── IUnitOfWork.cs
│
├── Authra.Infrastructure/
│   ├── DependencyInjection.cs
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Configurations/
│   └── Services/
│       ├── AuthService.cs
│       ├── TenantService.cs
│       └── PasswordHasher.cs
│
└── Authra.Api/
    ├── DependencyInjection.cs
    ├── Endpoints/
    └── Program.cs
```

**Note**: `AuthEndpoints.cs` uses namespace `Authra.Api.Infrastructure` (not `Authra.Api.Endpoints` like other endpoint files).

### Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| Domain | Entities, value objects, domain rules | None |
| Application | Service interfaces, DTOs, validators | Domain |
| Infrastructure | EF Core, service implementations | Domain, Application |
| Api | Minimal API endpoints, middleware | Application, Infrastructure |

### Rationale

1. **Clean Architecture**: Layer separation with dependency inversion (Domain has no dependencies)
2. **Feature folders**: Related code grouped by feature (Auth, Tenants) instead of technical concern (Services, DTOs, Interfaces scattered)
3. **Services pattern**: Traditional services per feature — straightforward, no ceremony
4. **Minimal DDD**: Domain layer with entities and value objects, but no aggregates/repositories overkill for MVP

### Trade-offs Accepted

- Services may grow large over time (mitigated: can split into smaller services per feature)
- No CQRS separation (acceptable for MVP scale)

## DI Registration Pattern

Each layer has a `DependencyInjection.cs` with extension on `IHostApplicationBuilder` (not `IServiceCollection`):

- **Namespace**: All use `namespace Microsoft.Extensions.DependencyInjection` (no extra usings needed in Program.cs)
- **Signature**: `public static void AddXxx(this IHostApplicationBuilder builder)` returning `void`
- **Options**: Use `.AddOptions<T>().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()` (not `Configure<T>()`)
- **Program.cs**: `builder.AddApplication()`, `builder.AddInfrastructure()`, `builder.AddApi()`

| Layer | Registers |
|-------|-----------|
| Application | FluentValidation validators |
| Infrastructure | DbContext, options binding, all service implementations, email |
| Api | JSON serialization, JWT auth, authorization, rate limiting, CORS, health checks, OpenAPI, error handling |

## Architectural Decision: Email Service

**Decision**: SMTP abstraction with Mailpit (dev) + Resend (prod)

**Date**: 2026-01-25

### Strategy

| Component | Technology | Purpose |
|-----------|------------|---------|
| SMTP Client | MailKit 4.10.0 | Send emails via SMTP |
| Templates | Scriban 6.5.2 | Render email templates |
| Dev/Test | Mailpit (Docker) | Catch emails, web UI |
| Production | Resend | 3,000 free emails/month |

### Mailpit (Development)

```yaml
# docker-compose.yml
services:
  mailpit:
    image: axllent/mailpit
    ports:
      - "8025:8025"  # Web UI
      - "1025:1025"  # SMTP
```

- Web UI: `http://localhost:8025`
- SMTP: `localhost:1025` (no auth needed)

### Abstractions

```csharp
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}
```

### Implementations by Environment

| Environment | IEmailSender | SMTP Host |
|-------------|--------------|-----------|
| Development | `SmtpEmailSender` | Mailpit (localhost:1025) |
| Tests | `InMemoryEmailSender` | N/A |
| Production | `SmtpEmailSender` | Resend SMTP |

### MVP Templates

| Template | Trigger |
|----------|---------|
| `password-reset` | POST /auth/forgot-password |
| `tenant-invite` | POST /tenants/{id}/members/invite |

### Rationale

1. **Mailpit**: Docker-based, catches all emails locally, web UI to inspect
2. **Resend**: Generous free tier (3,000/month), SMTP support, no vendor lock-in
3. **SMTP abstraction**: Same code for dev/prod, swap providers via config
4. **MailKit**: Industry standard, MIT license, 150M+ downloads
5. **Scriban**: Sandboxed templates, no ASP.NET dependency

### Trade-offs Accepted

- External provider for production (mitigated: can self-host Postal later)
- Resend newer than SendGrid (mitigated: SMTP standard, easy to switch)

### Deferred to v1.1

- Email queue (background jobs)
- Delivery tracking/webhooks
- Self-hosted email server (Postal)

## Architectural Decision: Configuration Management

**Decision**: 1Password CLI with secret references

**Date**: 2026-01-25

### Strategy

| Environment | Method | Auth |
|-------------|--------|------|
| Local dev | `op run --env-file=.env.development` | Personal 1Password |
| CI/CD | GitHub Action + Service Account | Service Account token |
| Production | `op run --env-file=.env.production` | Service Account |

### Secret Reference Format

```
op://<vault>/<item>/<field>
```

### Environment Files (committed to git)

```bash
# .env.development
DATABASE_URL="op://Authra/dev-database/connection-string"
JWT_SIGNING_KEY="op://Authra/dev-jwt/private-key"
RESEND_API_KEY="op://Authra/dev-resend/api-key"
```

### Running the Application

```bash
# Local dev
op run --env-file=.env.development -- dotnet run

# Production
op run --env-file=.env.production -- dotnet Authra.Api.dll
```

### CI/CD (GitHub Actions)

```yaml
- uses: 1password/load-secrets-action@v2
  with:
    export-env: true
  env:
    OP_SERVICE_ACCOUNT_TOKEN: ${{ secrets.OP_SERVICE_ACCOUNT_TOKEN }}
    DATABASE_URL: op://Authra/prod-database/connection-string
```

### .NET Integration

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables();  // op run injects secrets here
```

### Rationale

1. **Single tool**: Same pattern for dev, CI, prod
2. **Secrets never on disk**: Only references in env files (safe to commit)
3. **Team-friendly**: Share vault, everyone gets secrets automatically
4. **No cloud lock-in**: Can migrate to Vault/Azure KV later

### Trade-offs Accepted

- 1Password subscription required ($4/user/mo)
- `op run` wrapper needed to start app

### Deferred to v1.1

- Azure Key Vault / AWS Secrets Manager integration
- Secret rotation automation

## Architectural Decision: Error Handling

**Decision**: Hybrid approach — RFC 9457 Problem Details + FluentValidation + Domain Exceptions

**Date**: 2026-01-25

### Strategy

| Error Type | Approach |
|------------|----------|
| Request validation | FluentValidation → 400 Problem Details |
| Business rules | Domain exceptions → Problem Details |
| Unexpected errors | Global exception handler → 500 |

### Response Format (RFC 9457)

```json
{
  "type": "https://authra.io/errors/validation",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/v1/auth/register",
  "traceId": "00-abc123...",
  "errors": {
    "email": ["Email is already registered"]
  }
}
```

### Domain Exceptions

```csharp
public abstract class DomainException : Exception
{
    public abstract int StatusCode { get; }
    protected DomainException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    public override int StatusCode => 404;
    public NotFoundException(string entity, object id)
        : base($"{entity} with ID '{id}' was not found") { }
}

public class ConflictException : DomainException
{
    public override int StatusCode => 409;
    public ConflictException(string message) : base(message) { }
}
```

### Global Exception Handler

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var problemDetails = exception switch
        {
            DomainException ex => new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ex.GetType().Name.Replace("Exception", ""),
                Detail = ex.Message
            },
            _ => new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred"
            }
        };

        context.Response.StatusCode = problemDetails.Status ?? 500;
        await context.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }
}
```

### Registration

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

app.UseExceptionHandler();
```

### Rationale

1. **RFC 9457**: Industry standard error format, built into ASP.NET Core
2. **FluentValidation**: Declarative, testable validation rules
3. **Domain exceptions**: Clear, typed exceptions for business errors
4. **Global handler**: Consistent responses, no try/catch boilerplate

### Trade-offs Accepted

- Exceptions for control flow (acceptable for expected domain errors)
- No Result pattern (can adopt ErrorOr later if needed)

### Deferred to v1.1

- Structured error codes (`AUTH_001`, `TENANT_002`)
- Error localization/i18n
- Error tracking (Sentry, Application Insights)

## Research Documentation

All research findings, industry comparisons, and decision rationale are documented in [CLAUDE-RESEARCHED.md](./CLAUDE-RESEARCHED.md).

### Topics Covered

| Topic | Decision |
|-------|----------|
| Permission naming convention | Colon notation (`:`) with `action:resource` pattern |
| Session/token strategy | JWT access + opaque refresh tokens |
| Database selection | PostgreSQL |
| Identity model | Proposal B (Separated Identifiers and Credentials) |
| Technology stack | C# 14 / .NET 10, EF Core 10, PostgreSQL 18.1, Docker Ubuntu Chiseled |
| ID generation | UUID v7 with PostgreSQL native `uuidv7()`, Medo.Uuid7 for application |
| Password hashing | Argon2id with `IPasswordHasher` abstraction |
| Project structure | Pure Clean Architecture, feature folders, services |
| Email service | Mailpit (dev), Resend (prod), MailKit, Scriban |
| Configuration management | 1Password CLI with secret references |
| Error handling | RFC 9457 Problem Details, FluentValidation, domain exceptions |

## Working with This Repository

**Current state**: Design and architecture phase. The repository currently contains architectural decisions and design documents. Implementation/development will follow.

### For Architecture Work

1. Read the current proposals in CLAUDE.md design notes
2. Use `architect-reviewer` agent for systematic architecture evaluation
3. Use `research-analyst` agent for researching patterns, technologies, or industry practices
4. Use `api-designer` agent for API contract design
5. Document decisions with clear rationale and trade-off analysis

### For Implementation Work

1. Use `/scaffold-entity` to create entities from CLAUDE-DATA-MODEL.md
2. Use `/scaffold-feature` to create complete features with all layers
3. Use `/add-endpoint` to extend features with new endpoints
4. Use `/test-feature` to generate comprehensive tests
5. Use `dotnet-developer` agent for feature implementation
6. Use `ef-core-specialist` agent for database and EF Core work
7. Use `test-engineer` agent for test creation

### For Code Review

1. Use `/review` for general code quality review
2. Use `/security-review` for security-focused review
3. Use `/performance-check` for performance analysis
4. Use `code-reviewer` agent for detailed code review
5. Use `security-reviewer` agent for security audit
6. Use `performance-reviewer` agent for optimization recommendations

### Claude Code Configuration Files

```
.claude/
├── agents/                    # 9 custom agents
│   ├── api-designer.md
│   ├── architect-reviewer.md
│   ├── code-reviewer.md
│   ├── dotnet-developer.md
│   ├── ef-core-specialist.md
│   ├── performance-reviewer.md
│   ├── research-analyst.md
│   ├── security-reviewer.md
│   └── test-engineer.md
│
└── skills/
    ├── conventions/           # 6 auto-loaded convention skills
    │   ├── api-conventions/
    │   ├── authra-data-model/
    │   ├── authra-security/
    │   ├── clean-architecture/
    │   ├── dotnet-conventions/
    │   └── testing-patterns/
    │
    └── workflows/             # 7 slash command skills
        ├── add-endpoint/
        ├── performance-check/
        ├── review/
        ├── scaffold-entity/
        ├── scaffold-feature/
        ├── security-review/
        └── test-feature/
```
