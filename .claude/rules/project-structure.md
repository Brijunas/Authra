---
name: project-structure
description: Clean Architecture layout and DI registration pattern for Authra — feature folders, layer boundaries, IHostApplicationBuilder-based DI.
version: 2.2.0
paths:
  - "src/**/*"
  - "tests/**/*"
---

# Project Structure

**Decision**: Pure Clean Architecture with minimal DDD, feature folders in Application layer. **Date**: 2026-01-25.

## Structure

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

## Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| Domain | Entities, value objects, domain rules | None |
| Application | Service interfaces, DTOs, validators | Domain |
| Infrastructure | EF Core, service implementations | Domain, Application |
| Api | Minimal API endpoints, middleware | Application, Infrastructure |

## Rationale

1. **Clean Architecture**: Layer separation with dependency inversion (Domain has no dependencies)
2. **Feature folders**: Related code grouped by feature (Auth, Tenants) instead of technical concern (Services, DTOs, Interfaces scattered)
3. **Services pattern**: Traditional services per feature — straightforward, no ceremony
4. **Minimal DDD**: Domain layer with entities and value objects, no aggregates/repositories overkill for MVP

## Trade-offs

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
