namespace Authra.Application.Roles.DTOs;

public record CreateRoleRequest(
    string Code,
    string Name,
    string? Description = null,
    bool IsDefault = false,
    IReadOnlyList<string>? PermissionIds = null);
