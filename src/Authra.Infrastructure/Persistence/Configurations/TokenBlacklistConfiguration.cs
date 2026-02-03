using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TokenBlacklist.
/// MVP implementation for JWT revocation (replaced by Redis in v1.1).
/// </summary>
public class TokenBlacklistConfiguration : IEntityTypeConfiguration<TokenBlacklist>
{
    public void Configure(EntityTypeBuilder<TokenBlacklist> builder)
    {
        builder.ToTable("token_blacklist");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("blacklist_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(t => t.Jti)
            .HasColumnName("jti")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(t => t.Reason)
            .HasColumnName("reason")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id");

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // JWT ID must be unique
        builder.HasIndex(t => t.Jti)
            .IsUnique();

        // Index for cleanup job (expired tokens can be deleted)
        builder.HasIndex(t => t.ExpiresAt)
            .HasDatabaseName("ix_token_blacklist_expiry");

        // Check constraint for reason
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_token_blacklist_reason",
            "reason IN ('logout', 'password_change', 'admin', 'security')"));

        // Relationships (nullable FKs)
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
