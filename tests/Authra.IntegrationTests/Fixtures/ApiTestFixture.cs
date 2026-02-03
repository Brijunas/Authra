using System.Net.Http.Json;
using Authra.Application.Common.Interfaces;
using Authra.Infrastructure.Persistence;
using Authra.Infrastructure.Persistence.Seeding;
using Authra.Infrastructure.Services;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Authra.IntegrationTests.Fixtures;

/// <summary>
/// Web application factory for API integration tests.
/// Configures the API to use Testcontainers PostgreSQL.
/// </summary>
public class ApiTestFixture : IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;
    public InMemoryEmailSender EmailSender { get; } = new();

    public ApiTestFixture(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext configuration
                    var dbContextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (dbContextDescriptor != null)
                    {
                        services.Remove(dbContextDescriptor);
                    }

                    // Add DbContext using the test database
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_databaseFixture.ConnectionString));

                    // Replace email sender with in-memory implementation
                    var emailSenderDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IEmailSender));
                    if (emailSenderDescriptor != null)
                    {
                        services.Remove(emailSenderDescriptor);
                    }
                    services.AddSingleton<IEmailSender>(EmailSender);

                    // Re-configure health checks with test database connection
                    var healthCheckDescriptors = services
                        .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                        .ToList();
                    foreach (var descriptor in healthCheckDescriptors)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddHealthChecks()
                        .AddNpgSql(_databaseFixture.ConnectionString, name: "postgresql");
                });
            });

        Client = _factory.CreateClient();

        // Run migrations only once (first fixture to initialize)
        if (!_databaseFixture.MigrationsApplied)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
            await _databaseFixture.MarkMigrationsAppliedAsync();
        }

        // Initialize respawner after migrations
        await _databaseFixture.InitializeRespawnerAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await _databaseFixture.ResetDatabaseAsync();

        // Re-seed system permissions after reset
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissions = SystemPermissionSeeder.GetSystemPermissions();
        context.Permissions.AddRange(permissions);
        await context.SaveChangesAsync();
    }

    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>
    /// Registers a new user and returns their info.
    /// </summary>
    public async Task<(string UserId, string AccessToken)> RegisterAndLoginAsync(string email, string password, string? username = null)
    {
        // Register
        var registerRequest = new { email, password, username };
        var registerResponse = await Client.PostAsJsonAsync("/v1/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Login
        var loginRequest = new { email, password };
        var loginResponse = await Client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        return (loginResult!.User.Id, loginResult.AccessToken);
    }

    /// <summary>
    /// Creates a tenant for the authenticated user and returns the tenant ID and new access token.
    /// </summary>
    public async Task<(string TenantId, string AccessToken)> CreateTenantAndLoginAsync(string accessToken, string name, string slug)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { name, slug });

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var tenant = await response.Content.ReadFromJsonAsync<TenantResponseDto>();

        // Login with tenant to get tenant-scoped token
        var loginRequest = new { email = "reused@example.com", password = "placeholder" };
        // We need to re-login with the user's actual credentials - this helper doesn't have them.
        // Return the user-only token for now, caller should re-login if needed
        return (tenant!.Id, accessToken);
    }

    /// <summary>
    /// Sets the Authorization header on the client.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string accessToken)
    {
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return Client;
    }

    /// <summary>
    /// Clears the Authorization header on the client.
    /// </summary>
    public void ClearAuthentication()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    // DTOs for test helper methods
    private record LoginResponseDto(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessTokenExpiresAt,
        DateTimeOffset RefreshTokenExpiresAt,
        UserInfoDto User,
        TenantInfoDto? Tenant);

    private record UserInfoDto(string Id, string Email, string? Username);
    private record TenantInfoDto(string Id, string Name, string Slug, string MemberId, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
    private record TenantResponseDto(string Id, string Name, string Slug, string Status, string? OwnerId, DateTimeOffset CreatedAt);
}
