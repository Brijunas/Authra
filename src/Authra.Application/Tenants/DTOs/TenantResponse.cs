namespace Authra.Application.Tenants.DTOs;

public record TenantResponse(
    string Id,
    string Name,
    string Slug,
    string Status,
    string? OwnerId,
    DateTimeOffset CreatedAt);
