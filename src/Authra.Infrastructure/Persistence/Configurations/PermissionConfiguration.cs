using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Permission.
/// Handles both system permissions (TenantId = NULL) and tenant-defined permissions (v1.1).
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("permission_id")
            .HasDefaultValueSql("uuidv7()");

        // TenantId is nullable (NULL = system permission)
        builder.Property(p => p.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(p => p.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(p => p.Category)
            .HasColumnName("category")
            .HasMaxLength(100);

        builder.Property(p => p.IsSystem)
            .HasColumnName("is_system")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique index with NULLS NOT DISTINCT for PostgreSQL 15+
        // This ensures (TenantId, Code) is unique even when TenantId is NULL
        builder.HasIndex(p => new { p.TenantId, p.Code })
            .HasDatabaseName("uk_permissions_tenant_code")
            .IsUnique()
            .AreNullsDistinct(false); // PostgreSQL 15+ NULLS NOT DISTINCT

        // Index for system permission lookup by code
        builder.HasIndex(p => p.Code)
            .HasDatabaseName("ix_permissions_system_code")
            .HasFilter("tenant_id IS NULL");

        // Relationship with Tenant (optional)
        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with RolePermission configured in RolePermissionConfiguration
    }
}
