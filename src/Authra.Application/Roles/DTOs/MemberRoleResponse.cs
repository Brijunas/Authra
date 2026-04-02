namespace Authra.Application.Roles.DTOs;

public record MemberRoleResponse(
    string RoleId,
    string RoleCode,
    string RoleName,
    DateTimeOffset AssignedAt,
    string? AssignedById);
