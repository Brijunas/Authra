# Clean Architecture Conventions

This skill defines Clean Architecture patterns and layer boundaries for Authra.

## Project Structure

```
src/
├── Authra.Domain/           # Entities, value objects, domain rules
├── Authra.Application/      # Service interfaces, DTOs, validators
├── Authra.Infrastructure/   # EF Core, external services
└── Authra.Api/              # Minimal API endpoints, middleware
```

## Layer Dependencies

```
Api → Application → Domain
         ↓
    Infrastructure
```

- **Domain**: Zero dependencies
- **Application**: References Domain only
- **Infrastructure**: References Domain and Application
- **Api**: References Application and Infrastructure

## Domain Layer

### Location

`src/Authra.Domain/`

### Contents

```
Authra.Domain/
├── Entities/
│   ├── User.cs
│   ├── Tenant.cs
│   └── ...
├── ValueObjects/
│   ├── Email.cs
│   └── ...
├── Enums/
│   ├── IdentifierType.cs
│   └── ...
└── Exceptions/
    ├── DomainException.cs
    ├── NotFoundException.cs
    └── ConflictException.cs
```

### Rules

- No dependencies on other layers
- No EF Core references
- No ASP.NET Core references
- Pure C# with .NET BCL only
- Contains business rules and invariants

### Entity Example

```csharp
namespace Authra.Domain.Entities;

public class User
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserIdentifier> Identifiers { get; init; } = [];
    public PasswordAuth? PasswordAuth { get; set; }
    public ICollection<ExternalAuth> ExternalAuths { get; init; } = [];
    public ICollection<TenantMember> TenantMemberships { get; init; } = [];
}
```

### Value Object Example

```csharp
namespace Authra.Domain.ValueObjects;

public sealed record Email
{
    public string Value { get; }
    public string Normalized { get; }

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format", nameof(value));

        Value = value;
        Normalized = value.ToLowerInvariant().Trim();
    }
}
```

### Domain Exception Example

Each exception class goes in its own file:

`DomainException.cs`:
```csharp
namespace Authra.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}
```

`NotFoundException.cs`:
```csharp
namespace Authra.Domain.Exceptions;

public class NotFoundException(string entity, object id)
    : DomainException($"{entity} with ID '{id}' was not found")
{
    public override int StatusCode => 404;
}
```

`ConflictException.cs`:
```csharp
namespace Authra.Domain.Exceptions;

public class ConflictException(string message) : DomainException(message)
{
    public override int StatusCode => 409;
}
```

## Application Layer

### Location

`src/Authra.Application/`

### Contents (Feature Folders)

```
Authra.Application/
├── Auth/
│   ├── IAuthService.cs
│   ├── DTOs/
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   └── RegisterRequest.cs
│   └── Validators/
│       ├── LoginRequestValidator.cs
│       └── RegisterRequestValidator.cs
├── Tenants/
│   ├── ITenantService.cs
│   ├── DTOs/
│   └── Validators/
├── Organizations/
├── Roles/
└── Common/
    └── Interfaces/
        ├── IUnitOfWork.cs
        ├── IPasswordHasher.cs
        └── IEmailSender.cs
```

### Rules

- References Domain only
- Defines service interfaces (implemented in Infrastructure)
- Contains DTOs for API communication
- Contains FluentValidation validators
- No EF Core DbContext references
- No HTTP-specific code

### Service Interface Example

```csharp
namespace Authra.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<TokenPair> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(string refreshToken, CancellationToken ct);
}
```

### DTO Example

Each record goes in its own file:

`LoginRequest.cs`:
```csharp
namespace Authra.Application.Auth.DTOs;

public record LoginRequest(string Email, string Password);
```

`LoginResponse.cs`:
```csharp
namespace Authra.Application.Auth.DTOs;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    UserDto User);
```

`UserDto.cs`:
```csharp
namespace Authra.Application.Auth.DTOs;

public record UserDto(
    string Id,  // Prefixed: usr_xxx
    string Email,
    string? DisplayName);
```

### Validator Example

```csharp
namespace Authra.Application.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
```

## Infrastructure Layer

### Location

`src/Authra.Infrastructure/`

### Contents

```
Authra.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── UserConfiguration.cs
│   │   ├── TenantConfiguration.cs
│   │   └── ...
│   └── Interceptors/
│       └── TenantInterceptor.cs
└── Services/
    ├── AuthService.cs
    ├── TenantService.cs
    ├── TokenService.cs
    ├── PasswordHasher.cs
    └── EmailSender.cs
```

### Rules

- Implements Application layer interfaces
- Contains EF Core DbContext and configurations
- Contains external service integrations
- No business logic (delegate to Domain)

### Service Implementation Example

```csharp
namespace Authra.Infrastructure.Services;

public class AuthService(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken ct)
    {
        var identifier = await context.UserIdentifiers
            .Include(ui => ui.User)
                .ThenInclude(u => u.PasswordAuth)
            .FirstOrDefaultAsync(
                ui => ui.Type == IdentifierType.Email
                    && ui.ValueNormalized == request.Email.ToLowerInvariant(),
                ct);

        if (identifier?.User.PasswordAuth is null)
            throw new UnauthorizedException("Invalid credentials");

        var result = passwordHasher.Verify(
            request.Password,
            identifier.User.PasswordAuth.PasswordHash);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid credentials");

        // Generate tokens...
        return new LoginResponse(accessToken, refreshToken, userDto);
    }
}
```

## API Layer

### Location

`src/Authra.Api/`

### Contents

```
Authra.Api/
├── Endpoints/
│   ├── AuthEndpoints.cs
│   ├── TenantEndpoints.cs
│   ├── OrganizationEndpoints.cs
│   └── RoleEndpoints.cs
├── Middleware/
│   └── GlobalExceptionHandler.cs
└── Program.cs
```

### Rules

- Maps HTTP endpoints to Application services
- Contains ASP.NET Core configuration
- No business logic
- Handles HTTP concerns (status codes, headers)

### Endpoint Example

```csharp
namespace Authra.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AllowAnonymous()
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem()
            .ProducesProblem(401);

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .AllowAnonymous()
            .Produces<RegisterResponse>(201)
            .ProducesValidationProblem()
            .ProducesProblem(409);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IAuthService authService,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var response = await authService.LoginAsync(request, ct);
        return Results.Ok(response);
    }
}
```

### Program.cs Structure

```csharp
var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

// Middleware
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapOrganizationEndpoints();
app.MapRoleEndpoints();

app.Run();
```

## Dependency Injection Registration

Each layer registers its own services:

```csharp
// Authra.Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        return services;
    }
}

// Authra.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        return services;
    }
}
```
