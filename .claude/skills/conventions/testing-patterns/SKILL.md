# Testing Patterns

This skill defines testing conventions using xUnit v3, Testcontainers, and Respawn.

## Context7 Usage

When implementing tests, use Context7 MCP tools to query:
- `/xunit/xunit` - xUnit v3 patterns
- `/testcontainers/testcontainers-dotnet` - Testcontainers setup
- Query "Respawn" for database reset patterns

## Test Project Structure

```
tests/
├── Authra.UnitTests/
│   ├── Domain/
│   ├── Application/
│   └── Infrastructure/
└── Authra.IntegrationTests/
    ├── Fixtures/
    │   └── DatabaseFixture.cs
    ├── Auth/
    │   └── AuthEndpointsTests.cs
    ├── Tenants/
    └── ...
```

## xUnit v3 Conventions

### Test Class Naming

```csharp
// Unit tests: {ClassUnderTest}Tests
public class PasswordHasherTests { }
public class AuthServiceTests { }

// Integration tests: {Feature}EndpointsTests
public class AuthEndpointsTests { }
public class TenantEndpointsTests { }
```

### Test Method Naming

Pattern: `{Method}_{Scenario}_{ExpectedResult}`

```csharp
public class PasswordHasherTests
{
    [Fact]
    public void Hash_ValidPassword_ReturnsPhcString()

    [Fact]
    public void Verify_CorrectPassword_ReturnsSuccess()

    [Fact]
    public void Verify_WrongPassword_ReturnsFailed()

    [Fact]
    public void NeedsRehash_OutdatedParams_ReturnsTrue()
}
```

### Arrange-Act-Assert

```csharp
[Fact]
public void Hash_ValidPassword_ReturnsPhcString()
{
    // Arrange
    var hasher = new Argon2PasswordHasher();
    var password = "SecurePassword123!";

    // Act
    var hash = hasher.Hash(password);

    // Assert
    hash.Should().StartWith("$argon2id$");
    hash.Should().Contain("$m=47104,t=1,p=1$");
}
```

### Theory Tests

```csharp
[Theory]
[InlineData("")]
[InlineData(" ")]
[InlineData(null)]
public void Validate_EmptyEmail_ReturnsFailed(string? email)
{
    // Arrange
    var validator = new LoginRequestValidator();
    var request = new LoginRequest(email!, "password123");

    // Act
    var result = validator.Validate(request);

    // Assert
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Email");
}
```

## Testcontainers PostgreSQL

### Assembly Fixture (xUnit v3)

```csharp
[assembly: AssemblyFixture(typeof(DatabaseFixture))]

namespace Authra.IntegrationTests.Fixtures;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Run migrations
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Using Fixture in Tests

```csharp
public class AuthEndpointsTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Respawner _respawner = null!;

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }
}
```

## WebApplicationFactory

### Custom Factory

```csharp
public class AuthraWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public AuthraWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add test database
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }
}
```

### Integration Test Example

```csharp
public class AuthEndpointsTests(DatabaseFixture db) : IAsyncLifetime
{
    private AuthraWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new AuthraWebApplicationFactory(db.ConnectionString);
        _client = _factory.CreateClient();

        // Reset database before each test
        await ResetDatabaseAsync();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        await SeedUserAsync("test@example.com", "Password123!");
        var request = new LoginRequest("test@example.com", "Password123!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<LoginResponse>();
        content.Should().NotBeNull();
        content!.AccessToken.Should().NotBeNullOrEmpty();
        content.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        // Arrange
        await SeedUserAsync("test@example.com", "Password123!");
        var request = new LoginRequest("test@example.com", "WrongPassword!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        user.Identifiers.Add(new UserIdentifier
        {
            UserId = user.Id,
            Type = IdentifierType.Email,
            Value = email,
            ValueNormalized = email.ToLowerInvariant()
        });
        user.PasswordAuth = new PasswordAuth
        {
            UserId = user.Id,
            PasswordHash = hasher.Hash(password)
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}
```

## Respawn Database Reset

### Configuration

```csharp
var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
{
    DbAdapter = DbAdapter.Postgres,
    SchemasToInclude = ["public"],
    TablesToIgnore = ["__EFMigrationsHistory", "permissions"] // Seed data tables
});
```

### Reset Between Tests

```csharp
public async ValueTask DisposeAsync()
{
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();
    await _respawner.ResetAsync(connection);
}
```

## Mocking with NSubstitute

### Unit Test Example

```csharp
public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorized()
    {
        // Arrange
        var context = Substitute.For<AppDbContext>();
        var hasher = Substitute.For<IPasswordHasher>();
        var tokenService = Substitute.For<ITokenService>();

        hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(PasswordVerificationResult.Failed);

        var service = new AuthService(context, hasher, tokenService);

        // Act & Assert
        await service.Invoking(s => s.LoginAsync(
                new LoginRequest("test@example.com", "wrong"),
                CancellationToken.None))
            .Should()
            .ThrowAsync<UnauthorizedException>();
    }
}
```

## Fake Data with Bogus

```csharp
public class UserFaker : Faker<User>
{
    public UserFaker()
    {
        RuleFor(u => u.Id, f => Guid.NewGuid());
        RuleFor(u => u.CreatedAt, f => f.Date.Past());
    }
}

public class TenantFaker : Faker<Tenant>
{
    public TenantFaker()
    {
        RuleFor(t => t.Id, f => Guid.NewGuid());
        RuleFor(t => t.Name, f => f.Company.CompanyName());
        RuleFor(t => t.Slug, (f, t) => t.Name.ToLowerInvariant().Replace(" ", "-"));
        RuleFor(t => t.CreatedAt, f => f.Date.Past());
    }
}

// Usage
var user = new UserFaker().Generate();
var tenants = new TenantFaker().Generate(5);
```

## AwesomeAssertions Patterns

```csharp
// Object assertions
user.Should().NotBeNull();
user.Email.Should().Be("test@example.com");

// Collection assertions
roles.Should().HaveCount(3);
roles.Should().Contain(r => r.Code == "admin");
roles.Should().BeInAscendingOrder(r => r.Name);

// Exception assertions
await action.Should()
    .ThrowAsync<NotFoundException>()
    .WithMessage("*User*not found*");

// HTTP response assertions
response.StatusCode.Should().Be(HttpStatusCode.Created);
response.Headers.Location.Should().NotBeNull();

// JSON assertions
var content = await response.Content.ReadFromJsonAsync<UserDto>();
content.Should().BeEquivalentTo(expected, options =>
    options.Excluding(u => u.CreatedAt));
```

## Test Categories

Use traits to categorize tests:

```csharp
[Trait("Category", "Unit")]
public class PasswordHasherTests { }

[Trait("Category", "Integration")]
public class AuthEndpointsTests { }

[Trait("Category", "Slow")]
public class FullFlowTests { }
```

Run by category:

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category!=Slow"
```
