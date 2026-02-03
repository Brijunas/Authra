using Authra.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authra.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TenantMemberRole.
/// Junction table linking TenantMember to Role.
/// Tenant-scoped entity with TenantId denormalized for RLS.
/// </summary>
public class TenantMemberRoleConfiguration : IEntityTypeConfiguration<TenantMemberRole>
{
    public void Configure(EntityTypeBuilder<TenantMemberRole> builder)
    {
        builder.ToTable("tenant_member_roles");

        builder.HasKey(tmr => tmr.Id);

        builder.Property(tmr => tmr.Id)
            .HasColumnName("tenant_member_role_id")
            .HasDefaultValueSql("uuidv7()");

        builder.Property(tmr => tmr.TenantMemberId)
            .HasColumnName("tenant_member_id")
            .IsRequired();

        builder.Property(tmr => tmr.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(tmr => tmr.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(tmr => tmr.AssignedAt)
            .HasColumnName("assigned_at")
            .HasDefaultValueSql("now()");

        builder.Property(tmr => tmr.AssignedBy)
            .HasColumnName("assigned_by");

        builder.Property(tmr => tmr.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(tmr => tmr.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        // Unique constraint: one role assignment per member
        builder.HasIndex(tmr => new { tmr.TenantMemberId, tmr.RoleId })
            .HasDatabaseName("uk_tenant_member_roles_member_role")
            .IsUnique();

        // Index for member's roles lookup
        builder.HasIndex(tmr => tmr.TenantMemberId)
            .HasDatabaseName("ix_tenant_member_roles_member_id");

        // Index for role's assignments lookup
        builder.HasIndex(tmr => tmr.RoleId)
            .HasDatabaseName("ix_tenant_member_roles_role_id");

        // Index for RLS performance
        builder.HasIndex(tmr => tmr.TenantId)
            .HasDatabaseName("ix_tenant_member_roles_tenant_id");

        // Relationship with TenantMember - CASCADE delete
        builder.HasOne(tmr => tmr.TenantMember)
            .WithMany(tm => tm.RoleAssignments)
            .HasForeignKey(tmr => tmr.TenantMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Role - CASCADE delete
        builder.HasOne(tmr => tmr.Role)
            .WithMany(r => r.MemberRoles)
            .HasForeignKey(tmr => tmr.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with Tenant - CASCADE delete
        builder.HasOne(tmr => tmr.Tenant)
            .WithMany()
            .HasForeignKey(tmr => tmr.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship with AssignedBy TenantMember - SET NULL on delete
        builder.HasOne(tmr => tmr.AssignedByMember)
            .WithMany()
            .HasForeignKey(tmr => tmr.AssignedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
