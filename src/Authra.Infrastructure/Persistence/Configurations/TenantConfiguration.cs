using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("tenant_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.OwnerMemberId)
            .HasColumnName("owner_member_id");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("active")
            .IsRequired();

        builder.Property(t => t.AllowCustomPermissions)
            .HasColumnName("allow_custom_permissions")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.AllowOrgRestrictions)
            .HasColumnName("allow_org_restrictions")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint on slug (global uniqueness)
        builder.HasIndex(t => t.Slug)
            .HasDatabaseName("ix_tenants_slug")
            .IsUnique();

        // Check constraint for valid status
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tenants_status",
            "status IN ('active', 'suspended', 'deleted')"));

        // Relationships
        builder.HasMany(t => t.Members)
            .WithOne(m => m.Tenant)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Organizations)
            .WithOne(o => o.Tenant)
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Invites)
            .WithOne(i => i.Tenant)
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Owner relationship - DEFERRABLE constraint handled in migration
        // The FK is added separately to allow circular reference during creation
        builder.HasOne(t => t.OwnerMember)
            .WithOne()
            .HasForeignKey<Tenant>(t => t.OwnerMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
