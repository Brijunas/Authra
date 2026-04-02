namespace Authra.Application.Auth.DTOs;

public record TenantSelectionRequired(
    string UserId,
    IReadOnlyList<AvailableTenant> AvailableTenants);
