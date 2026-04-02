namespace Authra.Application.Roles.DTOs;

public record PermissionResponse(
    string Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    bool IsSystem);
