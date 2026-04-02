---
name: scaffold-feature
description: Scaffold a complete feature with service, DTOs, validators, endpoints, and tests
---

# Scaffold Feature

Creates a complete feature implementation across all Clean Architecture layers.

## Usage

```
/scaffold-feature <FeatureName> [--entity <EntityName>] [--endpoints <list>]
```

## Arguments

- `<FeatureName>`: Feature name in PascalCase (e.g., Tenants, Organizations, Auth)
- `--entity`: Optional entity name if different from feature name
- `--endpoints`: Optional comma-separated endpoint list (e.g., "list,get,create,update,delete")

## Default Endpoints

If `--endpoints` not specified, creates standard CRUD:
- `GET /v1/{resource}` - List with pagination
- `GET /v1/{resource}/{id}` - Get by ID
- `POST /v1/{resource}` - Create
- `PUT /v1/{resource}/{id}` - Update
- `DELETE /v1/{resource}/{id}` - Delete

## What This Skill Does

### Step 1: Create Application Layer

**Service Interface** - `src/Authra.Application/{Feature}/I{Feature}Service.cs`:

```csharp
namespace Authra.Application.{Feature};

public interface I{Feature}Service
{
    Task<PagedResponse<{Entity}Dto>> GetAllAsync(int limit, string? cursor, CancellationToken ct);
    Task<{Entity}Dto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<{Entity}Dto> CreateAsync(Create{Entity}Request request, CancellationToken ct);
    Task<{Entity}Dto> UpdateAsync(Guid id, Update{Entity}Request request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

**DTOs** - `src/Authra.Application/{Feature}/DTOs/`:

```csharp
// Create{Entity}Request.cs
public record Create{Entity}Request(
    string Name,
    string? Description);

// Update{Entity}Request.cs
public record Update{Entity}Request(
    string? Name,
    string? Description);

// {Entity}Dto.cs
public record {Entity}Dto(
    string Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

**Validators** - `src/Authra.Application/{Feature}/Validators/`:

```csharp
public class Create{Entity}RequestValidator : AbstractValidator<Create{Entity}Request>
{
    public Create{Entity}RequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
```

### Step 2: Create Infrastructure Layer

**Service Implementation** - `src/Authra.Infrastructure/Services/{Feature}Service.cs`:

```csharp
namespace Authra.Infrastructure.Services;

public class {Feature}Service(
    AppDbContext context,
    ITenantContext tenantContext) : I{Feature}Service
{
    public async Task<PagedResponse<{Entity}Dto>> GetAllAsync(
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        var query = context.{Entity}s.AsNoTracking();

        // Apply cursor pagination
        // Project to DTO
        // Return paged response
    }

    public async Task<{Entity}Dto> CreateAsync(
        Create{Entity}Request request,
        CancellationToken ct)
    {
        var entity = new {Entity}
        {
            Name = request.Name,
            Description = request.Description
        };

        context.{Entity}s.Add(entity);
        await context.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    // ... other methods
}
```

### Step 3: Create API Layer

**Endpoints** - `src/Authra.Api/Endpoints/{Feature}Endpoints.cs`:

```csharp
namespace Authra.Api.Endpoints;

public static class {Feature}Endpoints
{
    public static void Map{Feature}Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/{resource}")
            .WithTags("{Feature}")
            .RequireAuthorization();

        group.MapGet("/", GetAllAsync)
            .WithName("Get{Feature}")
            .Produces<PagedResponse<{Entity}Dto>>(200);

        group.MapGet("/{id}", GetByIdAsync)
            .WithName("Get{Entity}")
            .Produces<{Entity}Dto>(200)
            .ProducesProblem(404);

        group.MapPost("/", CreateAsync)
            .WithName("Create{Entity}")
            .Produces<{Entity}Dto>(201)
            .ProducesValidationProblem()
            .ProducesProblem(409);

        group.MapPut("/{id}", UpdateAsync)
            .WithName("Update{Entity}")
            .Produces<{Entity}Dto>(200)
            .ProducesValidationProblem()
            .ProducesProblem(404);

        group.MapDelete("/{id}", DeleteAsync)
            .WithName("Delete{Entity}")
            .Produces(204)
            .ProducesProblem(404);
    }

    private static async Task<IResult> GetAllAsync(
        [AsParameters] PaginationQuery pagination,
        I{Feature}Service service,
        CancellationToken ct)
    {
        var result = await service.GetAllAsync(pagination.Limit, pagination.Cursor, ct);
        return Results.Ok(result);
    }

    // ... other handlers
}
```

### Step 4: Register Dependencies

Update `src/Authra.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<I{Feature}Service, {Feature}Service>();
```

Update `src/Authra.Api/Program.cs`:

```csharp
app.Map{Feature}Endpoints();
```

### Step 5: Create Tests

**Unit Tests** - `tests/Authra.UnitTests/{Feature}/{Feature}ServiceTests.cs`
**Integration Tests** - `tests/Authra.IntegrationTests/{Feature}/{Feature}EndpointsTests.cs`

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `clean-architecture` | Layer boundaries, feature folders, DI |
| `api-conventions` | REST patterns, validation, error handling |
| `dotnet-conventions` | C# 14 coding standards |
| `authra-security` | Permission checks if applicable |
| `testing-patterns` | Test structure and patterns |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `api-designer` | Design API contract and endpoints |
| `dotnet-developer` | Implement Application and Infrastructure |
| `test-engineer` | Create unit and integration tests |
| `code-reviewer` | Validate output |

## Context7 Usage

Query these libraries:
- `/dotnet/aspnetcore` - Minimal API patterns
- `/fluentvalidation/fluentvalidation` - Validation patterns

## Example

```bash
/scaffold-feature Tenants --entity Tenant
```

**Generates:**

```
src/
├── Authra.Application/
│   └── Tenants/
│       ├── ITenantService.cs
│       ├── DTOs/
│       │   ├── CreateTenantRequest.cs
│       │   ├── UpdateTenantRequest.cs
│       │   └── TenantDto.cs
│       └── Validators/
│           ├── CreateTenantRequestValidator.cs
│           └── UpdateTenantRequestValidator.cs
├── Authra.Infrastructure/
│   └── Services/
│       └── TenantService.cs
└── Authra.Api/
    └── Endpoints/
        └── TenantEndpoints.cs

tests/
├── Authra.UnitTests/
│   └── Tenants/
│       └── TenantServiceTests.cs
└── Authra.IntegrationTests/
    └── Tenants/
        └── TenantEndpointsTests.cs
```

## Checklist

After running this skill, verify:

- [ ] Service interface in Application layer
- [ ] DTOs for request/response
- [ ] FluentValidation validators
- [ ] Service implementation in Infrastructure
- [ ] Minimal API endpoints
- [ ] DI registration
- [ ] Endpoint mapping in Program.cs
- [ ] Unit tests created
- [ ] Integration tests created
- [ ] All layers follow Clean Architecture
