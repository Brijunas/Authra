namespace Authra.Application.Organizations.DTOs;

public record CreateOrganizationRequest(
    string Name,
    string Slug);
