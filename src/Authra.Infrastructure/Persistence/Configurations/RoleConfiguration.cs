using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Role.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("role_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(r => r.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(r => r.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.IsSystem)
            .HasColumnName("is_system")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.IsRestricted)
            .HasColumnName("is_restricted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one role code per tenant
        builder.HasIndex(r => new { r.TenantId, r.Code })
            .HasDatabaseName("uk_roles_tenant_code")
            .IsUnique();

        // Index for default roles lookup (new member assignment)
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_roles_tenant_default")
            .HasFilter("is_default = true");

        // Relationship with Tenant
        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationships with RolePermission, TenantMemberRole, RoleOrganization
        // configured in their respective configurations
    }
}
