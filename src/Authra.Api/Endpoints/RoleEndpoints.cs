using System.Security.Claims;
using Authra.Api.Infrastructure;
using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Roles;
using Authra.Application.Roles.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Endpoints;

public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this WebApplication app)
    {
        // === Role CRUD ===

        var roleGroup = app.MapGroup("/v1/tenants/{tenantId}/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        // GET /v1/tenants/{tenantId}/roles
        roleGroup.MapGet("/", ListRolesAsync)
            .WithName("ListRoles")
            .RequireAuthorization("Permission:roles:read")
            .Produces<PagedResponse<RoleResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /v1/tenants/{tenantId}/roles
        roleGroup.MapPost("/", CreateRoleAsync)
            .WithName("CreateRole")
            .RequireAuthorization("Permission:roles:create")
            .Produces<RoleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /v1/tenants/{tenantId}/roles/{roleId}
        roleGroup.MapGet("/{roleId}", GetRoleAsync)
            .WithName("GetRole")
            .RequireAuthorization("Permission:roles:read")
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /v1/tenants/{tenantId}/roles/{roleId}
        roleGroup.MapPatch("/{roleId}", UpdateRoleAsync)
            .WithName("UpdateRole")
            .RequireAuthorization("Permission:roles:update")
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // DELETE /v1/tenants/{tenantId}/roles/{roleId}
        roleGroup.MapDelete("/{roleId}", DeleteRoleAsync)
            .WithName("DeleteRole")
            .RequireAuthorization("Permission:roles:delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // === Role Assignments ===

        var assignmentGroup = app.MapGroup("/v1/tenants/{tenantId}/members/{memberId}/roles")
            .WithTags("Role Assignments")
            .RequireAuthorization();

        // GET /v1/tenants/{tenantId}/members/{memberId}/roles
        assignmentGroup.MapGet("/", ListMemberRolesAsync)
            .WithName("ListMemberRoles")
            .RequireAuthorization("Permission:roles:read")
            .Produces<IReadOnlyList<MemberRoleResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /v1/tenants/{tenantId}/members/{memberId}/roles
        assignmentGroup.MapPost("/", AssignRoleAsync)
            .WithName("AssignRole")
            .RequireAuthorization("Permission:roles:assign")
            .Produces<MemberRoleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /v1/tenants/{tenantId}/members/{memberId}/roles/{roleId}
        assignmentGroup.MapDelete("/{roleId}", UnassignRoleAsync)
            .WithName("UnassignRole")
            .RequireAuthorization("Permission:roles:assign")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // === System Permissions ===

        // GET /v1/permissions - public endpoint to list system permissions
        app.MapGet("/v1/permissions", ListSystemPermissionsAsync)
            .WithName("ListPermissions")
            .WithTags("Permissions")
            .RequireAuthorization()
            .Produces<IReadOnlyList<PermissionResponse>>(StatusCodes.Status200OK);
    }

    // === Role CRUD ===

    private static async Task<IResult> ListRolesAsync(
        string tenantId,
        IRoleService roleService,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var pagination = new PaginationRequest(cursor, limit);
        var response = await roleService.ListRolesAsync(tenantGuid, pagination, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateRoleAsync(
        string tenantId,
        [FromBody] CreateRoleRequest request,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateCreateRoleRequest(request);

        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var response = await roleService.CreateRoleAsync(tenantGuid, request, ct);
        return Results.Created($"/v1/tenants/{tenantId}/roles/{response.Id}", response);
    }

    private static async Task<IResult> GetRoleAsync(
        string tenantId,
        string roleId,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var roleGuid = IdPrefix.DecodeRole(roleId);
        var response = await roleService.GetRoleAsync(tenantGuid, roleGuid, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateRoleAsync(
        string tenantId,
        string roleId,
        [FromBody] UpdateRoleRequest request,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var roleGuid = IdPrefix.DecodeRole(roleId);
        var response = await roleService.UpdateRoleAsync(tenantGuid, roleGuid, request, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteRoleAsync(
        string tenantId,
        string roleId,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var roleGuid = IdPrefix.DecodeRole(roleId);
        await roleService.DeleteRoleAsync(tenantGuid, roleGuid, ct);
        return Results.NoContent();
    }

    // === Role Assignments ===

    private static async Task<IResult> ListMemberRolesAsync(
        string tenantId,
        string memberId,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var memberGuid = IdPrefix.DecodeMember(memberId);
        var response = await roleService.ListMemberRolesAsync(tenantGuid, memberGuid, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> AssignRoleAsync(
        string tenantId,
        string memberId,
        [FromBody] AssignRoleRequest request,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateAssignRoleRequest(request);

        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var memberGuid = IdPrefix.DecodeMember(memberId);
        var roleGuid = IdPrefix.DecodeRole(request.RoleId);
        var assignedBy = ClaimsHelper.GetMemberId(user);

        var response = await roleService.AssignRoleAsync(tenantGuid, memberGuid, roleGuid, assignedBy, ct);
        return Results.Created($"/v1/tenants/{tenantId}/members/{memberId}/roles/{request.RoleId}", response);
    }

    private static async Task<IResult> UnassignRoleAsync(
        string tenantId,
        string memberId,
        string roleId,
        IRoleService roleService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var memberGuid = IdPrefix.DecodeMember(memberId);
        var roleGuid = IdPrefix.DecodeRole(roleId);
        await roleService.UnassignRoleAsync(tenantGuid, memberGuid, roleGuid, ct);
        return Results.NoContent();
    }

    // === System Permissions ===

    private static async Task<IResult> ListSystemPermissionsAsync(
        IRoleService roleService,
        CancellationToken ct)
    {
        var response = await roleService.ListSystemPermissionsAsync(ct);
        return Results.Ok(response);
    }

    // === Validation Helpers ===

    private static void ValidateCreateRoleRequest(CreateRoleRequest request)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("code", "Code is required"));
        }
        else if (request.Code.Length < 2)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("code", "Code must be at least 2 characters"));
        }
        else if (request.Code.Length > 50)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("code", "Code must not exceed 50 characters"));
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(request.Code, @"^[a-z0-9_-]+$"))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("code", "Code can only contain lowercase letters, numbers, underscores, and hyphens"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("name", "Name is required"));
        }
        else if (request.Name.Length < 2)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("name", "Name must be at least 2 characters"));
        }
        else if (request.Name.Length > 100)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("name", "Name must not exceed 100 characters"));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void ValidateAssignRoleRequest(AssignRoleRequest request)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("roleId", "Role ID is required"));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
