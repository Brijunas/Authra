---
name: test-feature
description: "Generate a comprehensive test suite for a feature including unit tests, integration tests with Testcontainers, and fixtures."
when_to_use: "When the user runs `/test-feature` or asks to generate tests for an existing feature."
version: 2.2.0
---

# Test Feature

Creates a comprehensive test suite for a feature with unit tests, integration tests, and test fixtures.

## Usage

```
/test-feature <FeatureName> [--unit-only] [--integration-only]
```

## Arguments

- `<FeatureName>`: Feature name (e.g., Tenants, Auth, Organizations)
- `--unit-only`: Generate only unit tests
- `--integration-only`: Generate only integration tests

## What This Skill Does

### Step 1: Analyze Feature

1. Read service interface and implementation
2. Identify all public methods to test
3. Understand dependencies for mocking
4. Identify test scenarios (success, validation, errors)

### Step 2: Create Test Fixtures (if needed)

**Database Fixture** - `tests/Authra.IntegrationTests/Fixtures/DatabaseFixture.cs`:

```csharp
[assembly: AssemblyFixture(typeof(DatabaseFixture))]

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

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

**Test Factory** - `tests/Authra.IntegrationTests/Fixtures/AuthraWebApplicationFactory.cs`:

```csharp
public class AuthraWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));
        });
    }
}
```

### Step 3: Create Fakers

**Entity Fakers** - `tests/Authra.IntegrationTests/Fixtures/Fakers/{Entity}Faker.cs`:

```csharp
public class {Entity}Faker : Faker<{Entity}>
{
    public {Entity}Faker()
    {
        RuleFor(e => e.Id, f => Guid.NewGuid());
        RuleFor(e => e.Name, f => f.Company.CompanyName());
        RuleFor(e => e.CreatedAt, f => f.Date.Past());
    }
}
```

### Step 4: Create Unit Tests

**Service Unit Tests** - `tests/Authra.UnitTests/{Feature}/{Feature}ServiceTests.cs`:

```csharp
public class {Feature}ServiceTests
{
    private readonly AppDbContext _context = Substitute.For<AppDbContext>();
    private readonly I{Dependency} _dependency = Substitute.For<I{Dependency}>();
    private readonly {Feature}Service _sut;

    public {Feature}ServiceTests()
    {
        _sut = new {Feature}Service(_context, _dependency);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        var entity = new {Entity}Faker().Generate();
        _context.{Entity}s.FindAsync(Arg.Any<object[]>())
            .Returns(entity);

        // Act
        var result = await _sut.GetByIdAsync(entity.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        _context.{Entity}s.FindAsync(Arg.Any<object[]>())
            .Returns((ValueTask<{Entity}?>)default);

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCreatedDto()
    {
        // Arrange
        var request = new Create{Entity}Request("Test Name", "Description");

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Name");
        _context.{Entity}s.Received(1).Add(Arg.Any<{Entity}>());
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        _context.{Entity}s.FindAsync(Arg.Any<object[]>())
            .Returns((ValueTask<{Entity}?>)default);

        // Act
        var act = () => _sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

**Validator Unit Tests** - `tests/Authra.UnitTests/{Feature}/Validators/Create{Entity}RequestValidatorTests.cs`:

```csharp
public class Create{Entity}RequestValidatorTests
{
    private readonly Create{Entity}RequestValidator _sut = new();

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        // Arrange
        var request = new Create{Entity}Request("Valid Name", "Description");

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_EmptyName_ReturnsInvalid(string? name)
    {
        // Arrange
        var request = new Create{Entity}Request(name!, null);

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
```

### Step 5: Create Integration Tests

**Endpoint Integration Tests** - `tests/Authra.IntegrationTests/{Feature}/{Feature}EndpointsTests.cs`:

```csharp
[Trait("Category", "Integration")]
public class {Feature}EndpointsTests(DatabaseFixture db) : IAsyncLifetime
{
    private AuthraWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Respawner _respawner = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new AuthraWebApplicationFactory(db.ConnectionString);
        _client = _factory.CreateClient();

        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory", "permissions"]
        });
    }

    [Fact]
    public async Task GetAll_NoItems_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/v1/{resource}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PagedResponse<{Entity}Dto>>();
        content!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsEntity()
    {
        // Arrange
        var entity = await Seed{Entity}Async();

        // Act
        var response = await _client.GetAsync($"/v1/{resource}/{entity.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<{Entity}Dto>();
        content!.Name.Should().Be(entity.Name);
    }

    [Fact]
    public async Task GetById_NonExistingId_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"/v1/{resource}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201()
    {
        // Arrange
        var request = new Create{Entity}Request("New Entity", "Description");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/{resource}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_InvalidRequest_Returns400()
    {
        // Arrange
        var request = new Create{Entity}Request("", null); // Invalid: empty name

        // Act
        var response = await _client.PostAsJsonAsync("/v1/{resource}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204()
    {
        // Arrange
        var entity = await Seed{Entity}Async();

        // Act
        var response = await _client.DeleteAsync($"/v1/{resource}/{entity.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);

        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<{Entity}> Seed{Entity}Async()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = new {Entity}Faker().Generate();
        context.{Entity}s.Add(entity);
        await context.SaveChangesAsync();

        return entity;
    }
}
```

## Convention Skills Applied

| Skill | Usage |
|-------|-------|
| `testing-conventions` | xUnit v3, Testcontainers, Respawn, assertions |
| `database-conventions` | Entity structure for fakers |
| `api-conventions` | Expected response formats |

## Agents to Invoke

| Agent | Purpose |
|-------|---------|
| `tester` | Generate all tests |
| `code-reviewer` | Review test quality |

## Context7 Usage

Query these libraries:
- `/xunit/xunit` - xUnit v3 patterns
- `/testcontainers/testcontainers-dotnet` - Testcontainers setup

## Example

```bash
/test-feature Tenants
```

**Generates:**

```
tests/
├── Authra.UnitTests/
│   └── Tenants/
│       ├── TenantServiceTests.cs
│       └── Validators/
│           ├── CreateTenantRequestValidatorTests.cs
│           └── UpdateTenantRequestValidatorTests.cs
└── Authra.IntegrationTests/
    ├── Fixtures/
    │   ├── DatabaseFixture.cs
    │   ├── AuthraWebApplicationFactory.cs
    │   └── Fakers/
    │       └── TenantFaker.cs
    └── Tenants/
        └── TenantEndpointsTests.cs
```

## Checklist

After running this skill, verify:

- [ ] Database fixture created (if first feature)
- [ ] WebApplicationFactory created (if first feature)
- [ ] Entity faker created
- [ ] Unit tests for service methods
- [ ] Unit tests for validators
- [ ] Integration tests for all endpoints
- [ ] Tests cover success scenarios
- [ ] Tests cover validation errors
- [ ] Tests cover not found scenarios
- [ ] Tests cover conflict scenarios
- [ ] Respawn configured for cleanup
- [ ] Tests are independent and repeatable
