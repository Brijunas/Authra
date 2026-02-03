using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TenantMember.
/// Handles the bridge between global User and tenant-scoped context.
/// </summary>
public class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        builder.ToTable("tenant_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("tenant_member_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("active")
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

        // Unique constraint: one membership per user per tenant
        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique();

        // Index for user lookup (user's tenant memberships)
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("ix_tenant_members_user_id");

        // Index for active members within a tenant
        builder.HasIndex(m => new { m.TenantId, m.Status })
            .HasDatabaseName("ix_tenant_members_status")
            .HasFilter("status = 'active'");

        // Check constraint for valid status
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_tenant_members_status",
            "status IN ('active', 'suspended', 'removed')"));

        // Relationships are configured in TenantConfiguration and UserConfiguration
        // to avoid duplicate configuration
    }
}
