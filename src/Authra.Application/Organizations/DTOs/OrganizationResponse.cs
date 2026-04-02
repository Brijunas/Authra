namespace Authra.Application.Organizations.DTOs;

public record OrganizationResponse(
    string Id,
    string TenantId,
    string Name,
    string Slug,
    string Status,
    int MemberCount,
    DateTimeOffset CreatedAt);
