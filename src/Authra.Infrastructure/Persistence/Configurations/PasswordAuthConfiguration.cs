using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class PasswordAuthConfiguration : IEntityTypeConfiguration<PasswordAuth>
{
    public void Configure(EntityTypeBuilder<PasswordAuth> builder)
    {
        builder.ToTable("password_auth");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("password_auth_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(p => p.UserId)
            .HasColumnName("user_id");

        builder.Property(p => p.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Algorithm)
            .HasColumnName("algorithm")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Params)
            .HasColumnName("params")
            .HasColumnType("jsonb");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // One password per user
        builder.HasIndex(p => p.UserId)
            .IsUnique();

        // Check constraint for valid algorithms
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_password_auth_algorithm",
            "algorithm IN ('argon2id', 'bcrypt', 'scrypt')"));
    }
}
