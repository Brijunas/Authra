namespace Authra.Application.Roles.DTOs;

// === Role ===

public record CreateRoleRequest(
    string Code,
    string Name,
    string? Description = null,
    bool IsDefault = false,
    IReadOnlyList<string>? PermissionIds = null);

public record UpdateRoleRequest(
    string? Name = null,
    string? Description = null,
    bool? IsDefault = null,
    IReadOnlyList<string>? PermissionIds = null);

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

// === Permission ===

public record PermissionResponse(
    string Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    bool IsSystem);

// === Role Assignment ===

public record AssignRoleRequest(
    string RoleId);

public record MemberRoleResponse(
    string RoleId,
    string RoleCode,
    string RoleName,
    DateTimeOffset AssignedAt,
    string? AssignedById);
