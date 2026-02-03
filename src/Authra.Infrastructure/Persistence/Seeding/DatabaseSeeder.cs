using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Authra.Infrastructure.Persistence.Seeding;

/// <summary>
/// Service for seeding the database with initial data.
/// Call SeedAsync() during application startup after migrations.
/// </summary>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds all required initial data if not already present.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSystemPermissionsAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the 19 MVP system permissions if they don't already exist.
    /// Idempotent - safe to call multiple times.
    /// </summary>
    private async Task SeedSystemPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _context.Permissions
            .Where(p => p.TenantId == null && p.IsSystem)
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        var systemPermissions = SystemPermissionSeeder.GetSystemPermissions();
        var newPermissions = systemPermissions
            .Where(p => !existingCodes.Contains(p.Code))
            .ToList();

        if (newPermissions.Count > 0)
        {
            _context.Permissions.AddRange(newPermissions);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} system permissions", newPermissions.Count);
        }
        else
        {
            _logger.LogDebug("All {Count} system permissions already exist", systemPermissions.Count);
        }
    }
}
