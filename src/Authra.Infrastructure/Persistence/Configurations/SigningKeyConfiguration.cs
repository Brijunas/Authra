using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SigningKey.
/// Global entity for JWT signing key rotation.
/// Uses human-readable KeyId as primary key (not UUID).
/// </summary>
public class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKey>
{
    public void Configure(EntityTypeBuilder<SigningKey> builder)
    {
        builder.ToTable("signing_keys");

        // Human-readable primary key (e.g., 'key-2026-01-25-001')
        builder.HasKey(k => k.KeyId);

        builder.Property(k => k.KeyId)
            .HasColumnName("key_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(k => k.Algorithm)
            .HasColumnName("algorithm")
            .HasMaxLength(20)
            .HasDefaultValue("ES256")
            .IsRequired();

        builder.Property(k => k.PublicKeyPem)
            .HasColumnName("public_key_pem")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(k => k.PrivateKeyEncrypted)
            .HasColumnName("private_key_encrypted")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(k => k.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(k => k.ActivatedAt)
            .HasColumnName("activated_at");

        builder.Property(k => k.RotatedOutAt)
            .HasColumnName("rotated_out_at");

        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        // Index for active signing keys (JWKS lookup)
        builder.HasIndex(k => k.Status)
            .HasDatabaseName("ix_signing_keys_status")
            .HasFilter("status IN ('active', 'rotate_out')");

        // Check constraints
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_signing_keys_status",
            "status IN ('pending', 'active', 'rotate_out', 'expired')"));

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_signing_keys_algorithm",
            "algorithm IN ('ES256', 'ES384', 'RS256')"));
    }
}
