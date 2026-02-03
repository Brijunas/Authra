namespace Authra.Domain.Entities;

/// <summary>
/// Workspace/organization boundary. Root entity for multi-tenancy.
/// Not a TenantEntity itself - it IS the tenant root.
/// </summary>
public class Tenant : Entity
{
    private readonly List<TenantMember> _members = [];
    private readonly List<Organization> _organizations = [];
    private readonly List<Invite> _invites = [];

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Owner member ID. Nullable to handle circular reference during creation.
    /// FK added with DEFERRABLE INITIALLY DEFERRED constraint.
    /// </summary>
    public Guid? OwnerMemberId { get; private set; }
    public TenantMember? OwnerMember { get; private set; }

    public string Status { get; private set; } = "active";

    /// <summary>
    /// v1.1 ready: When TRUE, tenant can create custom permissions.
    /// </summary>
    public bool AllowCustomPermissions { get; private set; }

    /// <summary>
    /// v1.1 ready: When TRUE, roles can be restricted to specific organizations.
    /// </summary>
    public bool AllowOrgRestrictions { get; private set; }

    public IReadOnlyCollection<TenantMember> Members => _members.AsReadOnly();
    public IReadOnlyCollection<Organization> Organizations => _organizations.AsReadOnly();
    public IReadOnlyCollection<Invite> Invites => _invites.AsReadOnly();

    private Tenant()
    {
        // EF Core constructor
    }

    public static Tenant Create(string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var tenant = new Tenant
        {
            Name = name.Trim(),
            Slug = NormalizeSlug(slug),
            Status = "active",
            AllowCustomPermissions = false,
            AllowOrgRestrictions = false
        };

        return tenant;
    }

    public TenantMember AddMember(User user)
    {
        var member = TenantMember.Create(Id, user.Id);
        _members.Add(member);
        return member;
    }

    public void SetOwner(TenantMember member)
    {
        if (member.TenantId != Id)
            throw new InvalidOperationException("Member does not belong to this tenant.");

        OwnerMemberId = member.Id;
        OwnerMember = member;
    }

    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void UpdateSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Slug = NormalizeSlug(slug);
    }

    public void Suspend()
    {
        if (Status == "deleted")
            throw new InvalidOperationException("Cannot suspend a deleted tenant.");

        Status = "suspended";
    }

    public void Activate()
    {
        if (Status == "deleted")
            throw new InvalidOperationException("Cannot activate a deleted tenant.");

        Status = "active";
    }

    public void MarkDeleted()
    {
        Status = "deleted";
    }

    public Organization AddOrganization(string name, string slug)
    {
        var organization = Organization.Create(Id, name, slug);
        _organizations.Add(organization);
        return organization;
    }

    public Invite CreateInvite(string email, Guid invitedByMemberId, DateTimeOffset expiresAt, string token, IEnumerable<Guid>? roleIds = null)
    {
        var invite = Invite.Create(Id, email, invitedByMemberId, expiresAt, token, roleIds);
        _invites.Add(invite);
        return invite;
    }

    private static string NormalizeSlug(string slug)
    {
        // Lowercase, trim, and ensure URL-safe
        return slug.ToLowerInvariant().Trim();
    }
}
