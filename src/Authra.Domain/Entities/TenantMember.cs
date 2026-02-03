namespace Authra.Domain.Entities;

/// <summary>
/// User membership in a tenant.
/// Bridges the global User entity to the tenant-scoped context.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class TenantMember : TenantEntity
{
    private readonly List<OrganizationMember> _organizationMemberships = [];
    private readonly List<Invite> _sentInvites = [];
    private readonly List<TenantMemberRole> _roleAssignments = [];

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;

    public Tenant Tenant { get; private set; } = null!;

    public string Status { get; private set; } = "active";
    public DateTimeOffset JoinedAt { get; private set; }

    public IReadOnlyCollection<OrganizationMember> OrganizationMemberships => _organizationMemberships.AsReadOnly();
    public IReadOnlyCollection<Invite> SentInvites => _sentInvites.AsReadOnly();
    public IReadOnlyCollection<TenantMemberRole> RoleAssignments => _roleAssignments.AsReadOnly();

    private TenantMember()
    {
        // EF Core constructor
    }

    internal static TenantMember Create(Guid tenantId, Guid userId)
    {
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            Status = "active",
            JoinedAt = DateTimeOffset.UtcNow
        };

        return member;
    }

    public void Suspend()
    {
        if (Status == "removed")
            throw new InvalidOperationException("Cannot suspend a removed member.");

        Status = "suspended";
    }

    public void Activate()
    {
        if (Status == "removed")
            throw new InvalidOperationException("Cannot activate a removed member.");

        Status = "active";
    }

    public void Remove()
    {
        Status = "removed";
    }

    public bool IsActive => Status == "active";

    /// <summary>
    /// Assigns a role to this tenant member.
    /// </summary>
    public TenantMemberRole AssignRole(Role role, Guid? assignedBy = null)
    {
        if (role.TenantId != TenantId)
            throw new InvalidOperationException("Role does not belong to this member's tenant.");

        var roleAssignment = TenantMemberRole.Create(Id, role.Id, TenantId, assignedBy);
        _roleAssignments.Add(roleAssignment);
        return roleAssignment;
    }
}
