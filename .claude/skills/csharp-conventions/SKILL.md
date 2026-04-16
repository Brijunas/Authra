---
name: csharp-conventions
description: "C# 14 and .NET 10 coding standards for Authra — language features, naming, nullable annotations, async patterns, primary constructors, required members."
when_to_use: "When writing or editing any C# code in Authra — covers C# 14 language features, nullable annotations, naming, async patterns."
paths:
  - "src/**/*.cs"
  - "tests/**/*.cs"
version: 2.2.0
---

# .NET Conventions

This skill defines C# 14 and .NET 10 coding standards for the Authra project.

## Context7 Usage

When implementing .NET patterns, use Context7 MCP tools to query:
- `/dotnet/runtime` - .NET 10 runtime features
- `/dotnet/aspnetcore` - ASP.NET Core 10 patterns

## Language Version

Target C# 14 with .NET 10:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

## File-Scoped Namespaces

Always use file-scoped namespaces:

```csharp
// Good
namespace Authra.Domain.Entities;

public class User { }

// Bad
namespace Authra.Domain.Entities
{
    public class User { }
}
```

## Nullable Reference Types

Enable nullable reference types project-wide. Be explicit about nullability:

```csharp
// Good
public string? MiddleName { get; set; }
public string Email { get; set; } = string.Empty;

// Bad - null warnings suppressed
public string Email { get; set; } = null!;
```

## Primary Constructors

Use primary constructors for dependency injection:

```csharp
// Good
public class AuthService(
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    AppDbContext context) : IAuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var hash = passwordHasher.Hash(request.Password);
        // ...
    }
}

// Bad - verbose constructor
public class AuthService : IAuthService
{
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }
}
```

## Records for DTOs

Use records for immutable data transfer objects. Each record goes in its own file named after the type:

`LoginRequest.cs`:
```csharp
public record LoginRequest(string Email, string Password);
```

`LoginResponse.cs`:
```csharp
public record LoginResponse(string AccessToken, string RefreshToken);
```

`UserDto.cs`:
```csharp
public record UserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    DateTime CreatedAt);
```

## Collection Expressions

Use collection expressions (C# 12+):

```csharp
// Good
List<string> roles = ["admin", "user"];
string[] scopes = ["read", "write"];

// Bad
var roles = new List<string> { "admin", "user" };
```

## Pattern Matching

Leverage pattern matching for cleaner code:

```csharp
// Good
return exception switch
{
    NotFoundException ex => Results.NotFound(ex.Message),
    ConflictException ex => Results.Conflict(ex.Message),
    ValidationException ex => Results.BadRequest(ex.Errors),
    _ => Results.Problem()
};

// Good - property patterns
if (user is { DeletedAt: null, IsActive: true })
{
    // ...
}
```

## Raw String Literals

Use raw string literals for multi-line strings:

```csharp
var sql = """
    SELECT u.id, u.email
    FROM users u
    WHERE u.tenant_id = @tenantId
    """;
```

## Required Members

Use `required` modifier for mandatory properties:

```csharp
public class CreateTenantRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
}
```

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `UserService` |
| Interfaces | IPascalCase | `IUserService` |
| Methods | PascalCase | `GetUserAsync` |
| Properties | PascalCase | `CreatedAt` |
| Private fields | _camelCase | `_passwordHasher` |
| Parameters | camelCase | `userId` |
| Local variables | camelCase | `currentUser` |
| Constants | PascalCase | `MaxRetryAttempts` |
| Async methods | Suffix with Async | `LoginAsync` |

## Async/Await

Always use async/await for I/O operations:

```csharp
// Good
public async Task<User?> GetUserAsync(Guid id, CancellationToken ct)
{
    return await _context.Users.FindAsync([id], ct);
}

// Bad - blocking
public User? GetUser(Guid id)
{
    return _context.Users.Find(id);
}
```

Always accept and forward `CancellationToken`:

```csharp
public async Task<IResult> Handle(
    GetUserQuery query,
    CancellationToken ct)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == query.Id, ct);
    // ...
}
```

## Extension Methods

Place in `Extensions` namespace with descriptive names:

```csharp
namespace Authra.Application.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim?.Value ?? throw new InvalidOperationException());
    }
}
```

## Guard Clauses

Use ArgumentNullException.ThrowIfNull and similar:

```csharp
public void Process(User user, string token)
{
    ArgumentNullException.ThrowIfNull(user);
    ArgumentException.ThrowIfNullOrWhiteSpace(token);
    // ...
}
```

## Minimal APIs

Use endpoint grouping and typed results:

```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem()
            .ProducesProblem(401);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        return result.Match(
            success => Results.Ok(success),
            error => Results.Problem(error));
    }
}
```

## Dependency Injection

Register services by feature:

```csharp
public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        return services;
    }
}
```

## Configuration

Use strongly-typed configuration:

```csharp
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int AccessTokenMinutes { get; init; }
    public required int RefreshTokenDays { get; init; }
}

// Registration
services.Configure<JwtSettings>(
    configuration.GetSection(JwtSettings.SectionName));
```

## Logging

Use structured logging with Serilog:

```csharp
public class AuthService(ILogger<AuthService> logger)
{
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        logger.LogInformation(
            "Login attempt for {Email}",
            request.Email);

        // ... on failure
        logger.LogWarning(
            "Failed login attempt for {Email} from {IpAddress}",
            request.Email,
            ipAddress);
    }
}
```

Never log sensitive data (passwords, tokens, PII).
