namespace Authra.IntegrationTests.Fixtures;

/// <summary>
/// Base class for integration tests that need database access.
/// Automatically resets the database before each test.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected DatabaseFixture Database { get; }

    protected IntegrationTestBase(DatabaseFixture database)
    {
        Database = database;
    }

    public virtual async ValueTask InitializeAsync()
    {
        await Database.ResetDatabaseAsync();
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
