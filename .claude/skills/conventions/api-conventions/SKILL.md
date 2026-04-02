# API Conventions

This skill defines REST API patterns for Authra's Minimal API endpoints.

## Context7 Usage

When implementing API endpoints, use Context7 MCP tools to query:
- `/dotnet/aspnetcore` - ASP.NET Core Minimal APIs
- `/fluentvalidation/fluentvalidation` - Request validation

## URL Structure

### Base URL

```
/v1/{resource}
```

### Versioning

URL path versioning under `/v1` prefix:

```
GET  /v1/tenants
POST /v1/auth/login
GET  /v1/organizations/{id}
```

## HTTP Methods

| Method | Usage | Example |
|--------|-------|---------|
| GET | Retrieve resource(s) | `GET /v1/tenants` |
| POST | Create resource | `POST /v1/tenants` |
| PUT | Full update | `PUT /v1/tenants/{id}` |
| PATCH | Partial update | `PATCH /v1/tenants/{id}` |
| DELETE | Remove resource | `DELETE /v1/tenants/{id}` |

## Resource Naming

- Plural nouns for collections: `/tenants`, `/organizations`
- Lowercase with hyphens: `/tenant-members`, `/refresh-token`
- Nested resources for relationships: `/tenants/{id}/members`

## ID Prefixes

API responses use type-prefixed IDs:

| Prefix | Resource |
|--------|----------|
| `usr_` | User |
| `tnt_` | Tenant |
| `mbr_` | TenantMember |
| `org_` | Organization |
| `rol_` | Role |
| `prm_` | Permission |
| `inv_` | Invite |
| `req_` | Request ID |

### Encoding/Decoding

```csharp
public static class IdEncoder
{
    private static readonly Dictionary<string, string> Prefixes = new()
    {
        ["User"] = "usr_",
        ["Tenant"] = "tnt_",
        ["TenantMember"] = "mbr_",
        ["Organization"] = "org_",
        ["Role"] = "rol_",
        ["Permission"] = "prm_",
        ["Invite"] = "inv_"
    };

    public static string Encode(string type, Guid id)
    {
        var prefix = Prefixes[type];
        var base64 = Convert.ToBase64String(id.ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        return $"{prefix}{base64}";
    }

    public static Guid Decode(string prefixedId)
    {
        var base64 = prefixedId[4..]
            .Replace("-", "+")
            .Replace("_", "/");

        var padding = (4 - base64.Length % 4) % 4;
        base64 += new string('=', padding);

        return new Guid(Convert.FromBase64String(base64));
    }
}
```

## Status Codes

| Code | Usage |
|------|-------|
| 200 | Success (GET, PUT, PATCH) |
| 201 | Created (POST) |
| 204 | No Content (DELETE) |
| 400 | Validation error |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not found |
| 409 | Conflict |
| 429 | Rate limited |
| 500 | Server error |

## Error Response Format (RFC 9457)

```json
{
  "type": "https://authra.io/errors/validation",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/v1/auth/register",
  "traceId": "00-abc123def456...",
  "errors": {
    "email": ["Email is already registered"],
    "password": ["Password must be at least 8 characters"]
  }
}
```

### Error Types

| Type | Status | Usage |
|------|--------|-------|
| `/errors/validation` | 400 | Request validation failed |
| `/errors/unauthorized` | 401 | Authentication required |
| `/errors/forbidden` | 403 | Insufficient permissions |
| `/errors/not-found` | 404 | Resource doesn't exist |
| `/errors/conflict` | 409 | Resource conflict |
| `/errors/rate-limit` | 429 | Too many requests |

## Cursor-Based Pagination

### Request

```
GET /v1/tenants?limit=20&cursor=eyJpZCI6Ijk5OSJ9
```

| Parameter | Default | Max | Description |
|-----------|---------|-----|-------------|
| `limit` | 20 | 100 | Items per page |
| `cursor` | null | - | Opaque cursor from previous response |

### Response

```json
{
  "data": [
    { "id": "tnt_abc123", "name": "Acme Corp" },
    { "id": "tnt_def456", "name": "Beta Inc" }
  ],
  "pagination": {
    "hasMore": true,
    "nextCursor": "eyJpZCI6ImRlZjQ1NiJ9"
  }
}
```

### Implementation

```csharp
public record PagedResponse<T>(
    IEnumerable<T> Data,
    PaginationInfo Pagination);

public record PaginationInfo(
    bool HasMore,
    string? NextCursor);

public static class PaginationExtensions
{
    public static async Task<PagedResponse<T>> ToPagedAsync<T>(
        this IQueryable<T> query,
        int limit,
        string? cursor,
        Func<T, string> cursorSelector,
        CancellationToken ct) where T : class
    {
        if (cursor is not null)
        {
            var decoded = DecodeCursor(cursor);
            query = query.Where(/* cursor condition */);
        }

        var items = await query
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);

        var nextCursor = hasMore
            ? EncodeCursor(cursorSelector(items.Last()))
            : null;

        return new PagedResponse<T>(items, new PaginationInfo(hasMore, nextCursor));
    }
}
```

## Request Validation

### FluentValidation Integration

```csharp
public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens");
    }
}
```

### Endpoint Validation

```csharp
private static async Task<IResult> CreateTenantAsync(
    CreateTenantRequest request,
    IValidator<CreateTenantRequest> validator,
    ITenantService tenantService,
    CancellationToken ct)
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var tenant = await tenantService.CreateAsync(request, ct);
    return Results.Created($"/v1/tenants/{tenant.Id}", tenant);
}
```

## Endpoint Group Pattern

```csharp
public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("/", GetAllAsync)
            .WithName("GetTenants")
            .Produces<PagedResponse<TenantDto>>(200);

        group.MapGet("/{id}", GetByIdAsync)
            .WithName("GetTenant")
            .Produces<TenantDto>(200)
            .ProducesProblem(404);

        group.MapPost("/", CreateAsync)
            .WithName("CreateTenant")
            .Produces<TenantDto>(201)
            .ProducesValidationProblem()
            .ProducesProblem(409);

        group.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateTenant")
            .Produces<TenantDto>(200)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        group.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteTenant")
            .Produces(204)
            .ProducesProblem(404);
    }
}
```

## Authentication Endpoints

Authentication endpoints are anonymous:

```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization();

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous();

        group.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous();
    }
}
```

## Permission-Protected Endpoints

```csharp
group.MapGet("/", GetAllMembersAsync)
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("accounts:read"));

group.MapPost("/invite", InviteMemberAsync)
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("accounts:invite"));
```

## Request/Response DTOs

### Create Request

```csharp
public record CreateTenantRequest(
    string Name,
    string Slug,
    string? Description);
```

### Update Request

```csharp
public record UpdateTenantRequest(
    string? Name,
    string? Description);  // Partial update, null = no change
```

### Response DTO

```csharp
public record TenantDto(
    string Id,          // Prefixed: tnt_xxx
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

## Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Type = "https://authra.io/errors/rate-limit",
            Title = "Rate Limit Exceeded",
            Status = 429,
            Detail = "Too many requests. Please try again later."
        }, token);
    };
});
```

## OpenAPI Documentation

```csharp
builder.Services.AddOpenApi();

app.MapOpenApi();
app.MapScalarApiReference();  // Scalar UI at /scalar
```

Endpoint documentation:

```csharp
group.MapPost("/login", LoginAsync)
    .WithName("Login")
    .WithSummary("Authenticate user")
    .WithDescription("Validates credentials and returns access/refresh tokens")
    .Produces<LoginResponse>(200)
    .ProducesValidationProblem()
    .ProducesProblem(401);
```
