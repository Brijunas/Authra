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

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

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
}
