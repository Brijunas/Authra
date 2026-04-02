namespace Authra.Application.Tenants.DTOs;

public record UpdateTenantRequest(
    string? Name = null,
    string? Slug = null);
