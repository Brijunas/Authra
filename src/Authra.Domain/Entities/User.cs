namespace Authra.Domain.Entities;

/// <summary>
/// Core identity anchor. Global entity (not tenant-scoped).
/// A user can have multiple identifiers (email, username, phone) and
/// belong to multiple tenants.
/// </summary>
public class User : Entity
{
    private readonly List<UserIdentifier> _identifiers = [];
    private readonly List<TenantMember> _tenantMemberships = [];

    public IReadOnlyCollection<UserIdentifier> Identifiers => _identifiers.AsReadOnly();
    public IReadOnlyCollection<TenantMember> TenantMemberships => _tenantMemberships.AsReadOnly();

    public PasswordAuth? PasswordAuth { get; private set; }

    private User()
    {
        // EF Core constructor
    }

    public static User Create()
    {
        var user = new User();
        return user;
    }

    public UserIdentifier AddIdentifier(string type, string value)
    {
        var normalizedValue = NormalizeIdentifierValue(type, value);
        var identifier = UserIdentifier.Create(Id, type, normalizedValue);
        _identifiers.Add(identifier);
        return identifier;
    }

    public void SetPasswordAuth(PasswordAuth passwordAuth)
    {
        PasswordAuth = passwordAuth;
    }

    private static string NormalizeIdentifierValue(string type, string value)
    {
        return type.ToLowerInvariant() switch
        {
            "email" => value.ToLowerInvariant().Trim(),
            "username" => value.ToLowerInvariant().Trim(),
            "phone" => NormalizePhoneNumber(value),
            _ => value.Trim()
        };
    }

    private static string NormalizePhoneNumber(string phone)
    {
        // Remove all non-digit characters except leading +
        var normalized = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return normalized;
    }
}
