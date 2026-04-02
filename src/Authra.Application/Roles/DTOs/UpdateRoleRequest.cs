namespace Authra.Application.Roles.DTOs;

public record UpdateRoleRequest(
    string? Name = null,
    string? Description = null,
    bool? IsDefault = null,
    IReadOnlyList<string>? PermissionIds = null);
