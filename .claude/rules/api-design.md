---
name: api-design
description: REST API patterns for Authra — URL versioning, cursor pagination, RFC 9457 Problem Details error handling, FluentValidation, domain exceptions.
version: 2.2.0
paths:
  - "src/Authra.Api/Endpoints/**/*"
  - "src/Authra.Application/**/*Validator*.cs"
  - "src/Authra.Application/**/DTOs/**/*"
---

# API Design

REST API conventions. Full endpoint specification lives in [CLAUDE-API.md](../../CLAUDE-API.md).

## REST Conventions

- **URL versioning**: All endpoints under `/v1` prefix
- **Cursor-based pagination**: For all list endpoints
- **Structured errors**: RFC 9457 Problem Details with codes and request IDs

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

## Error Handling — Hybrid RFC 9457 + FluentValidation + Domain Exceptions

**Decision**: Hybrid approach. **Date**: 2026-01-25.

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

**Trade-offs**: Exceptions for control flow (acceptable for expected domain errors). No Result pattern (can adopt ErrorOr later if needed).

**Deferred to v1.1**: Structured error codes (`AUTH_001`, `TENANT_002`), localization/i18n, error tracking (Sentry, Application Insights).

## Deferred endpoints (v1.1)

- Session management endpoints (`/auth/sessions`)
- Tenant suspension/deletion
- Audit log endpoints
- External OAuth providers
- API keys
- Webhooks
