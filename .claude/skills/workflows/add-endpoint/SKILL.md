---
name: add-endpoint
description: Add a new REST endpoint to an existing feature
---

# Add Endpoint

Adds a new API endpoint to an existing feature with DTOs, validation, service method, and tests.

## Usage

```
/add-endpoint "<METHOD> /v1/{path}" [--feature <FeatureName>] [--permission <permission>]
```

## Arguments

- `"<METHOD> /v1/{path}"`: HTTP method and path (e.g., "POST /v1/tenants/{id}/members")
- `--feature`: Feature name if not inferrable from path
- `--permission`: Permission required (e.g., "accounts:invite")

## What This Skill Does

### Step 1: Analyze Existing Feature

1. Identify the feature from the URL path
2. Read existing service interface and implementation
3. Understand the entity and data model
4. Check existing DTOs and validators

### Step 2: Create Request/Response DTOs

If the endpoint requires new DTOs, create them in `src/Authra.Application/{Feature}/DTOs/`:

```csharp
// {Action}{Entity}Request.cs
public record {Action}{Entity}Request(
    string Property1,
    string? Property2);

// {Action}{Entity}Response.cs (if different from standard DTO)
public record {Action}{Entity}Response(
    string Id,
    string Message);
```

### Step 3: Create Validator

Create FluentValidation validator in `src/Authra.Application/{Feature}/Validators/`:

```csharp
public class {Action}{Entity}RequestValidator : AbstractValidator<{Action}{Entity}Request>
{
    public {Action}{Entity}RequestValidator()
    {
        RuleFor(x => x.Property1)
            .NotEmpty()
            .MaximumLength(100);
    }
}
```

### Step 4: Add Service Method

Add interface method to `src/Authra.Application/{Feature}/I{Feature}Service.cs`:

```csharp
Task<{Response}> {Action}Async({Request} request, CancellationToken ct);
```

Add implementation to `src/Authra.Infrastructure/Services/{Feature}Service.cs`:

```csharp
public async Task<{Response}> {Action}Async(
    {Request} request,
    CancellationToken ct)
{
    // Implementation
}
```

### Step 5: Add Endpoint

Add to existing endpoint group in `src/Authra.Api/Endpoints/{Feature}Endpoints.cs`:

```csharp
group.Map{Method}("/{path}", {Action}Async)
    .WithName("{ActionName}")
    .Produces<{Response}>({StatusCode})
    .ProducesValidationProblem()
    .ProducesProblem(404);

// If permission required
group.Map{Method}("/{path}", {Action}Async)
    .WithName("{ActionName}")
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("{permission}"))
    .Produces<{Response}>({StatusCode});
```

Add handler method:

```csharp
private static async Task<IResult> {Action}Async(
    {RouteParameters},
    {Request} request,
    IValidator<{Request}> validator,
    I{Feature}Service service,
    CancellationToken ct)
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid)
        return Results.ValidationProblem(validation.ToDictionary());

    var result = await service.{Action}Async(request, ct);
    return Results.Ok(result);
}
```

### Step 6: Add Integration Test

Add test to `tests/Authra.IntegrationTests/{Feature}/{Feature}EndpointsTests.cs`:

```csharp
[Fact]
public async Task {Action}_{Scenario}_Returns{StatusCode}()
{
    // Arrange
    await SeedDataAsync();
    var request = new {Request}("value1", "value2");

    // Act
    var response = await _client.{Method}AsJsonAsync("/v1/{path}", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.{Expected});
}
```

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `api-conventions` | REST patterns, validation, status codes |
| `authra-security` | Permission checking |
| `dotnet-conventions` | C# coding standards |
| `testing-patterns` | Integration test patterns |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `api-designer` | Design endpoint contract |
| `dotnet-developer` | Implement endpoint |
| `security-reviewer` | Validate permission requirements |
| `test-engineer` | Create integration test |

## Context7 Usage

Query these libraries:
- `/dotnet/aspnetcore` - Minimal API patterns
- `/fluentvalidation/fluentvalidation` - Validation patterns

## Example

```bash
/add-endpoint "POST /v1/tenants/{tenantId}/members/invite" --permission "accounts:invite"
```

**Generates/Updates:**

`src/Authra.Application/Tenants/DTOs/InviteMemberRequest.cs`:
```csharp
public record InviteMemberRequest(
    string Email,
    string? Role);
```

`src/Authra.Application/Tenants/DTOs/InviteResponse.cs`:
```csharp
public record InviteResponse(
    string Id,
    string Email,
    string Status,
    DateTime ExpiresAt);
```

`src/Authra.Application/Tenants/Validators/InviteMemberRequestValidator.cs`:
```csharp
public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Role)
            .Must(BeValidRole)
            .When(x => x.Role is not null);
    }
}
```

Updates `ITenantService.cs`:
```csharp
Task<InviteResponse> InviteMemberAsync(Guid tenantId, InviteMemberRequest request, CancellationToken ct);
```

Updates `TenantService.cs`:
```csharp
public async Task<InviteResponse> InviteMemberAsync(
    Guid tenantId,
    InviteMemberRequest request,
    CancellationToken ct)
{
    // Validate tenant exists
    // Check user not already member
    // Create invite
    // Send email
    // Return response
}
```

Updates `TenantEndpoints.cs`:
```csharp
group.MapPost("/{tenantId}/members/invite", InviteMemberAsync)
    .WithName("InviteTenantMember")
    .RequireAuthorization()
    .WithMetadata(new RequirePermissionAttribute("accounts:invite"))
    .Produces<InviteResponse>(201)
    .ProducesValidationProblem()
    .ProducesProblem(404)
    .ProducesProblem(409);
```

## Checklist

After running this skill, verify:

- [ ] Request DTO created (if needed)
- [ ] Response DTO created (if needed)
- [ ] Validator created
- [ ] Service interface updated
- [ ] Service implementation added
- [ ] Endpoint added to group
- [ ] Handler method implemented
- [ ] Permission check added (if required)
- [ ] Integration test created
- [ ] Proper status codes documented
