namespace Authra.Application.Tenants.DTOs;

public record CreateInviteRequest(
    string Email,
    IReadOnlyList<string>? RoleIds = null);
