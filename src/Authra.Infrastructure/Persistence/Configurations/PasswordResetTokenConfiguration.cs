using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("password_reset_token_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(t => t.UserId)
            .HasColumnName("user_id");

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.CreatedByIp)
            .HasColumnName("created_by_ip")
            .HasColumnType("inet");

        // Token hash must be unique
        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        // Index for unexpired tokens lookup
        builder.HasIndex(t => t.UserId)
            .HasFilter("used_at IS NULL");

        // Index for cleanup job
        builder.HasIndex(t => t.ExpiresAt)
            .HasFilter("used_at IS NULL");

        // Relationship
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
