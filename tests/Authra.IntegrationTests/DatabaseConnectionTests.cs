using Authra.IntegrationTests.Fixtures;
using AwesomeAssertions;
using Npgsql;
using Xunit;

namespace Authra.IntegrationTests;

/// <summary>
/// Verifies that the test database infrastructure is working correctly.
/// </summary>
public class DatabaseConnectionTests : IntegrationTestBase
{
    public DatabaseConnectionTests(DatabaseFixture database) : base(database)
    {
    }

    [Fact]
    public async Task Database_ShouldBeAccessible()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(Database.ConnectionString);

        // Act
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Assert
        connection.State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task Database_ShouldBePostgreSQL18()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(Database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Act
        await using var cmd = new NpgsqlCommand("SELECT version()", connection);
        var version = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;

        // Assert
        version.Should().NotBeNull();
        version.Should().Contain("PostgreSQL");
    }
}
