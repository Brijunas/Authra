using Authra.Domain.Exceptions;

namespace Authra.Application.Common;

/// <summary>
/// Utility for encoding/decoding prefixed IDs for API presentation.
/// Format: {prefix}_{guid-without-dashes}
/// </summary>
public static class IdPrefix
{
    public const string User = "usr";
    public const string Tenant = "tnt";
    public const string Member = "mbr";
    public const string Organization = "org";
    public const string Role = "rol";
    public const string Permission = "prm";
    public const string Invite = "inv";
    public const string Request = "req";

    /// <summary>
    /// Encodes a GUID with a type prefix.
    /// </summary>
    public static string Encode(string prefix, Guid id) => $"{prefix}_{id:N}";

    /// <summary>
    /// Decodes a prefixed ID string to a GUID.
    /// </summary>
    public static Guid Decode(string prefixedId, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(prefixedId))
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID: cannot be empty");
        }

        var parts = prefixedId.Split('_', 2);
        if (parts.Length != 2)
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID format: {prefixedId}");
        }

        if (parts[0] != expectedPrefix)
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID prefix: expected '{expectedPrefix}', got '{parts[0]}'");
        }

        if (!Guid.TryParse(parts[1], out var id))
        {
            throw new ValidationException($"Invalid {expectedPrefix} ID: {prefixedId}");
        }

        return id;
    }

    /// <summary>
    /// Tries to decode a prefixed ID string to a GUID.
    /// </summary>
    public static bool TryDecode(string prefixedId, string expectedPrefix, out Guid id)
    {
        id = Guid.Empty;

        if (string.IsNullOrEmpty(prefixedId))
            return false;

        var parts = prefixedId.Split('_', 2);
        if (parts.Length != 2 || parts[0] != expectedPrefix)
            return false;

        return Guid.TryParse(parts[1], out id);
    }

    // Convenience methods
    public static string EncodeUser(Guid id) => Encode(User, id);
    public static string EncodeTenant(Guid id) => Encode(Tenant, id);
    public static string EncodeMember(Guid id) => Encode(Member, id);
    public static string EncodeOrganization(Guid id) => Encode(Organization, id);
    public static string EncodeRole(Guid id) => Encode(Role, id);
    public static string EncodePermission(Guid id) => Encode(Permission, id);
    public static string EncodeInvite(Guid id) => Encode(Invite, id);

    public static Guid DecodeUser(string id) => Decode(id, User);
    public static Guid DecodeTenant(string id) => Decode(id, Tenant);
    public static Guid DecodeMember(string id) => Decode(id, Member);
    public static Guid DecodeOrganization(string id) => Decode(id, Organization);
    public static Guid DecodeRole(string id) => Decode(id, Role);
    public static Guid DecodePermission(string id) => Decode(id, Permission);
    public static Guid DecodeInvite(string id) => Decode(id, Invite);
}
