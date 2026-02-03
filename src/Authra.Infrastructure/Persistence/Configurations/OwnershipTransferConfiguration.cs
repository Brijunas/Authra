using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for OwnershipTransfer.
/// Audit trail for tenant ownership changes.
/// Uses ON DELETE RESTRICT for member references to preserve audit trail.
/// </summary>
public class OwnershipTransferConfiguration : IEntityTypeConfiguration<OwnershipTransfer>
{
    public void Configure(EntityTypeBuilder<OwnershipTransfer> builder)
    {
        builder.ToTable("ownership_transfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("transfer_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.FromMemberId)
            .HasColumnName("from_member_id")
            .IsRequired();

        builder.Property(t => t.ToMemberId)
            .HasColumnName("to_member_id")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(t => t.InitiatedAt)
            .HasColumnName("initiated_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.CompletedByMemberId)
            .HasColumnName("completed_by_member_id");

        builder.Property(t => t.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(t => t.CancelReason)
            .HasColumnName("cancel_reason")
            .HasColumnType("text");

        builder.Property(t => t.InitiatedByIp)
            .HasColumnName("initiated_by_ip")
            .HasColumnType("inet");

        builder.Property(t => t.CompletedByIp)
            .HasColumnName("completed_by_ip")
            .HasColumnType("inet");

        // v1.1 ready columns
        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(t => t.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Check constraint for status
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_ownership_transfers_status",
            "status IN ('pending', 'completed', 'cancelled', 'expired')"));

        // Relationships
        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // FromMember and ToMember use RESTRICT to preserve audit trail
        builder.HasOne(t => t.FromMember)
            .WithMany()
            .HasForeignKey(t => t.FromMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToMember)
            .WithMany()
            .HasForeignKey(t => t.ToMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // CompletedByMember uses SET NULL to keep history
        builder.HasOne(t => t.CompletedByMember)
            .WithMany()
            .HasForeignKey(t => t.CompletedByMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
