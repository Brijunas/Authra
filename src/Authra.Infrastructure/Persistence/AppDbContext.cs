using Authra.Application.Common.Interfaces;
using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Persistence;

/// <summary>
/// Main database context for Authra.
/// Applies configurations from the Configurations folder.
/// </summary>
public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly ITenantContext? _tenantContext;
    private readonly IDateTimeProvider? _dateTimeProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider) : base(options)
    {
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // Identity Layer (Global)
    public DbSet<User> Users => Set<User>();
    public DbSet<UserIdentifier> UserIdentifiers => Set<UserIdentifier>();
    public DbSet<PasswordAuth> PasswordAuths => Set<PasswordAuth>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Tenant Layer (RLS Enforced)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Use snake_case naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table names are already set in configurations
            // This handles any unmapped properties
            foreach (var property in entity.GetProperties())
            {
                if (string.IsNullOrEmpty(property.GetColumnName()))
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var now = _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = now;
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
