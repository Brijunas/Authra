using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("organization_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(o => o.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(o => o.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(o => o.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("active")
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: slug unique within tenant
        builder.HasIndex(o => new { o.TenantId, o.Slug })
            .IsUnique();

        // Check constraint for valid status
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_organizations_status",
            "status IN ('active', 'archived', 'deleted')"));

        // Relationships
        builder.HasMany(o => o.Members)
            .WithOne(m => m.Organization)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
