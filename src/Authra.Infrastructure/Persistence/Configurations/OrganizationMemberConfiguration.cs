using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("organization_member_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(m => m.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(m => m.TenantMemberId)
            .HasColumnName("tenant_member_id")
            .IsRequired();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.JoinedAt)
            .HasColumnName("joined_at")
            .HasDefaultValueSql("now()");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one membership per tenant member per organization
        builder.HasIndex(m => new { m.OrganizationId, m.TenantMemberId })
            .IsUnique();

        // Index for member's organizations lookup
        builder.HasIndex(m => m.TenantMemberId)
            .HasDatabaseName("ix_organization_members_tenant_member_id");

        // Index for RLS performance
        builder.HasIndex(m => m.TenantId)
            .HasDatabaseName("ix_organization_members_tenant_id");

        // Relationships
        builder.HasOne(m => m.TenantMember)
            .WithMany(tm => tm.OrganizationMemberships)
            .HasForeignKey(m => m.TenantMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Tenant)
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
