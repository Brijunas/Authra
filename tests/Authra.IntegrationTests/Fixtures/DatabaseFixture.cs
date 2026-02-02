using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Authra.IntegrationTests.Fixtures;

/// <summary>
/// Shared database fixture for integration tests using Testcontainers.
/// Uses PostgreSQL 18 with Respawn for fast test isolation.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("authra_test")
        .WithUsername("authra_test")
        .WithPassword("authra_test_password")
        .Build();

    private Respawner? _respawner;
    private bool _hasTablesInitialized;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Initializes Respawner after tables have been created (e.g., after migrations).
    /// Call this after applying EF Core migrations.
    /// </summary>
    public async Task InitializeRespawnerAsync()
    {
        if (_hasTablesInitialized)
            return;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });

        _hasTablesInitialized = true;
    }

    /// <summary>
    /// Resets the database to a clean state between tests.
    /// Only works after InitializeRespawnerAsync has been called.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
            return; // Skip reset if Respawner not initialized (no tables yet)

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
