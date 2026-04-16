---
name: tech-stack
description: Authra technology stack — C# 14/.NET 10, EF Core 10, PostgreSQL 18.1, NuGet packages, Docker Ubuntu Chiseled, testing stack.
version: 2.2.0
---

# Technology Stack

**Decision**: C# 14 / .NET 10 with PostgreSQL 18.1, Docker deployment. **Date**: 2026-01-25.

## Core Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Language | C# | 14 |
| Framework | ASP.NET Core Minimal APIs | 10.0 |
| Runtime | .NET | 10.0 |
| ORM | Entity Framework Core | 10.0 |
| Database | PostgreSQL | 18.1 |
| Container | Docker (Ubuntu Chiseled) | Latest |

## NuGet Packages (MVP)

### Database Access

| Package | Version | Purpose |
|---------|---------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 | EF Core 10 PostgreSQL provider |
| `Npgsql` | 10.0.0 | PostgreSQL driver |

### JWT & Cryptography

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.IdentityModel.JsonWebTokens` | 8.15.0 | JWT creation/validation (ES256) |
| `Konscious.Security.Cryptography.Argon2` | 1.3.1 | Argon2id password hashing |
| `Medo.Uuid7` | 3.2.0 | UUID v7 generation (application-side) |

### API & Validation

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | OpenAPI generation |
| `Scalar.AspNetCore` | Latest | API documentation UI (replaced Swashbuckle) |
| `FluentValidation` | 12.1.1 | Request validation |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | DI integration |

### Observability

| Package | Version | Purpose |
|---------|---------|---------|
| `Serilog.AspNetCore` | 10.0.0 | Structured logging |
| `Serilog.Sinks.Console` | Latest | Console sink |
| `AspNetCore.HealthChecks.NpgSql` | 9.0.0 | PostgreSQL health check |

### Testing

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

## Built-in .NET 10 Features (no packages needed)

| Feature | Usage |
|---------|-------|
| Rate Limiting | `AddRateLimiter()` middleware |
| CORS | `AddCors()` |
| Response Compression | `AddResponseCompression()` |
| Secure Random | `RandomNumberGenerator.GetBytes()` |
| ECDSA Keys | `ECDsa.Create(ECCurve.NamedCurves.nistP256)` |

## Docker Configuration

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

**Chiseled image features**:
- No shell, no package manager (attackers cannot execute commands)
- Non-root user by default (`app` user)
- ~110MB image size (vs ~220MB standard)
- Minimal attack surface

## Testing Strategy

**Testcontainers**: Spins up real PostgreSQL 18.1 in Docker during tests — no mocking database behavior, catches real SQL issues.

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

## Deferred to v1.1

| Component | Package |
|-----------|---------|
| Redis Cache | `Microsoft.Extensions.Caching.StackExchangeRedis` |
| PostgreSQL Logging | `Serilog.Sinks.Postgresql.Alternative` |
| Metrics | `prometheus-net.AspNetCore` |
| Distributed Tracing | `OpenTelemetry.Instrumentation.AspNetCore` |
| Email Service | SendGrid SDK or MailKit (already in MVP) |
| Native AOT | `PublishAot=true` with `runtime-deps:10.0-noble-chiseled` |
