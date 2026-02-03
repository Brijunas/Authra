using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RefreshToken.
/// Handles token storage with rotation tracking and reuse detection.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("refresh_token_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.TenantMemberId)
            .HasColumnName("tenant_member_id")
            .IsRequired();

        builder.Property(t => t.FamilyId)
            .HasColumnName("family_id")
            .HasDefaultValueSql("uuidv7()")
            .IsRequired();

        builder.Property(t => t.Generation)
            .HasColumnName("generation")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(t => t.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(255);

        builder.Property(t => t.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("inet");

        builder.Property(t => t.IssuedAt)
            .HasColumnName("issued_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(t => t.RevokedReason)
            .HasColumnName("revoked_reason")
            .HasMaxLength(50);

        builder.Property(t => t.AbsoluteExpiresAt)
            .HasColumnName("absolute_expires_at")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Token hash must be unique
        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        // Index for reuse detection (same family)
        builder.HasIndex(t => t.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        // Index for active tokens lookup
        builder.HasIndex(t => new { t.UserId, t.TenantId })
            .HasDatabaseName("ix_refresh_tokens_active")
            .HasFilter("revoked_at IS NULL");

        // Check constraint for revoke reasons
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_refresh_tokens_revoked_reason",
            "revoked_reason IS NULL OR revoked_reason IN ('logout', 'rotation', 'reuse_detected', 'admin', 'password_change')"));

        // Relationships
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.TenantMember)
            .WithMany()
            .HasForeignKey(t => t.TenantMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
