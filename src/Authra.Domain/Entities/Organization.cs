namespace Authra.Domain.Entities;

/// <summary>
/// Work context within a tenant. Organizations provide sub-grouping
/// for members within a tenant.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class Organization : TenantEntity
{
    private readonly List<OrganizationMember> _members = [];

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Status { get; private set; } = "active";

    public Tenant Tenant { get; private set; } = null!;
    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    private Organization()
    {
        // EF Core constructor
    }

    internal static Organization Create(Guid tenantId, string name, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var organization = new Organization
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = NormalizeSlug(slug),
            Status = "active"
        };

        return organization;
    }

    public OrganizationMember AddMember(TenantMember tenantMember)
    {
        if (tenantMember.TenantId != TenantId)
            throw new InvalidOperationException("Tenant member does not belong to this organization's tenant.");

        var member = OrganizationMember.Create(Id, tenantMember.Id, TenantId);
        _members.Add(member);
        return member;
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

    public void Archive()
    {
        if (Status == "deleted")
            throw new InvalidOperationException("Cannot archive a deleted organization.");

        Status = "archived";
    }

    public void Activate()
    {
        if (Status == "deleted")
            throw new InvalidOperationException("Cannot activate a deleted organization.");

        Status = "active";
    }

    public void MarkDeleted()
    {
        Status = "deleted";
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.ToLowerInvariant().Trim();
    }
}
