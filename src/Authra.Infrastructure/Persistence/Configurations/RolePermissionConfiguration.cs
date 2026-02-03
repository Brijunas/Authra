using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RolePermission.
/// Junction table linking Role to Permission.
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Id)
            .HasColumnName("role_permission_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(rp => rp.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(rp => rp.PermissionId)
            .HasColumnName("permission_id")
            .IsRequired();

        builder.Property(rp => rp.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(rp => rp.GrantedAt)
            .HasColumnName("granted_at")
            .HasDefaultValueSql("now()");

        // CreatedAt maps to GrantedAt for this entity
        builder.Property(rp => rp.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(rp => rp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one permission per role
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .HasDatabaseName("uk_role_permissions_role_permission")
            .IsUnique();

        // Index for role's permissions lookup
        builder.HasIndex(rp => rp.RoleId)
            .HasDatabaseName("ix_role_permissions_role_id");

        // Index for permission usage lookup (for RESTRICT delete check)
        builder.HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("ix_role_permissions_permission_id");

        // Index for RLS performance
        builder.HasIndex(rp => rp.TenantId)
            .HasDatabaseName("ix_role_permissions_tenant_id");

        // Relationship with Role - CASCADE delete
        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Permission - RESTRICT delete (prevent deletion if in use)
        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship with Tenant - CASCADE delete
        builder.HasOne(rp => rp.Tenant)
            .WithMany()
            .HasForeignKey(rp => rp.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
