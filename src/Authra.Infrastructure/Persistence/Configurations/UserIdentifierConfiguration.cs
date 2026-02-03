using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class UserIdentifierConfiguration : IEntityTypeConfiguration<UserIdentifier>
{
    public void Configure(EntityTypeBuilder<UserIdentifier> builder)
    {
        builder.ToTable("user_identifiers");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("user_identifier_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(i => i.UserId)
            .HasColumnName("user_id");

        builder.Property(i => i.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.ValueNormalized)
            .HasColumnName("value_normalized")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one identifier value per type
        builder.HasIndex(i => new { i.Type, i.ValueNormalized })
            .IsUnique();

        // Check constraint for valid types
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_user_identifiers_type",
            "type IN ('email', 'username', 'phone')"));

        // Index for user lookup
        builder.HasIndex(i => i.UserId);
    }
}
