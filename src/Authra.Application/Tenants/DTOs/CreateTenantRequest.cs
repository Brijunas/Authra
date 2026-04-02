namespace Authra.Application.Tenants.DTOs;

public record CreateTenantRequest(
    string Name,
    string Slug);
