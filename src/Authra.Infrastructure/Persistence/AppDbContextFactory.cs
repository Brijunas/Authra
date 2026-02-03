using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Authra.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for AppDbContext.
/// Used by EF Core tools for migrations.
/// Run with: op run --env-file=.env.development -- dotnet ef ...
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Build connection string from environment variables (injected by op run)
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5433";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "authra";
        var username = Environment.GetEnvironmentVariable("DB_USER") ?? throw new InvalidOperationException("DB_USER environment variable is required. Run with: op run --env-file=.env.development -- dotnet ef ...");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable is required. Run with: op run --env-file=.env.development -- dotnet ef ...");

        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });

        return new AppDbContext(optionsBuilder.Options);
    }
}
