namespace Authra.Application.Roles.DTOs;

public record RoleResponse(
    string Id,
    string TenantId,
    string Code,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsSystem,
    IReadOnlyList<PermissionResponse> Permissions,
    DateTimeOffset CreatedAt);
