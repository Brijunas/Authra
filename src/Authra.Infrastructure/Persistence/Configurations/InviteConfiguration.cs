using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invites");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("invite_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.InvitedByMemberId)
            .HasColumnName("invited_by_member_id")
            .IsRequired();

        builder.Property(i => i.RoleIds)
            .HasColumnName("role_ids")
            .HasColumnType("uuid[]")
            .HasDefaultValueSql("'{}'");

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint on token (global uniqueness)
        builder.HasIndex(i => i.Token)
            .HasDatabaseName("ix_invites_token")
            .IsUnique();

        // Unique constraint: one pending invite per email per tenant
        builder.HasIndex(i => new { i.TenantId, i.Email })
            .IsUnique();

        // Index for pending invites by tenant
        builder.HasIndex(i => i.TenantId)
            .HasDatabaseName("ix_invites_tenant_pending")
            .HasFilter("status = 'pending'");

        // Check constraint for valid status
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_invites_status",
            "status IN ('pending', 'accepted', 'expired', 'cancelled')"));

        // Relationships
        builder.HasOne(i => i.InvitedByMember)
            .WithMany(m => m.SentInvites)
            .HasForeignKey(i => i.InvitedByMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
