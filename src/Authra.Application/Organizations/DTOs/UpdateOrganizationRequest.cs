namespace Authra.Application.Organizations.DTOs;

public record UpdateOrganizationRequest(
    string? Name = null,
    string? Slug = null);
