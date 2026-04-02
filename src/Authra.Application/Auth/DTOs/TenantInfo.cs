namespace Authra.Application.Auth.DTOs;

public record TenantInfo(
    string Id,
    string Name,
    string Slug,
    string MemberId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
