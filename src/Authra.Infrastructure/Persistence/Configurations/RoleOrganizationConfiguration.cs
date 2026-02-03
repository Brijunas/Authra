using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RoleOrganization.
/// Junction table for role restriction to specific organizations (v1.1 ready).
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class RoleOrganizationConfiguration : IEntityTypeConfiguration<RoleOrganization>
{
    public void Configure(EntityTypeBuilder<RoleOrganization> builder)
    {
        builder.ToTable("role_organizations");

        builder.HasKey(ro => ro.Id);

        builder.Property(ro => ro.Id)
            .HasColumnName("role_organization_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(ro => ro.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(ro => ro.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(ro => ro.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(ro => ro.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(ro => ro.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one organization restriction per role
        builder.HasIndex(ro => new { ro.RoleId, ro.OrganizationId })
            .HasDatabaseName("uk_role_organizations_role_org")
            .IsUnique();

        // Index for RLS performance
        builder.HasIndex(ro => ro.TenantId)
            .HasDatabaseName("ix_role_organizations_tenant_id");

        // Relationship with Role - CASCADE delete
        builder.HasOne(ro => ro.Role)
            .WithMany(r => r.OrganizationRestrictions)
            .HasForeignKey(ro => ro.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Organization - CASCADE delete
        builder.HasOne(ro => ro.Organization)
            .WithMany()
            .HasForeignKey(ro => ro.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Tenant - CASCADE delete
        builder.HasOne(ro => ro.Tenant)
            .WithMany()
            .HasForeignKey(ro => ro.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
