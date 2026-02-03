using System.Security.Claims;
using Authra.Api.Infrastructure;
using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Tenants;
using Authra.Application.Tenants.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        // POST /v1/tenants - Create tenant (no special permission needed, any authenticated user)
        group.MapPost("/", CreateTenantAsync)
            .WithName("CreateTenant")
            .Produces<TenantResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /v1/tenants/{id}
        group.MapGet("/{id}", GetTenantAsync)
            .WithName("GetTenant")
            .RequireAuthorization("Permission:tenant:read")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /v1/tenants/{id}
        group.MapPatch("/{id}", UpdateTenantAsync)
            .WithName("UpdateTenant")
            .RequireAuthorization("Permission:tenant:update")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /v1/tenants/{id}/transfer-ownership
        group.MapPost("/{id}/transfer-ownership", TransferOwnershipAsync)
            .WithName("TransferOwnership")
            .RequireAuthorization("Permission:tenant:transfer")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // === Members ===

        // GET /v1/tenants/{id}/members
        group.MapGet("/{id}/members", ListMembersAsync)
            .WithName("ListMembers")
            .RequireAuthorization("Permission:accounts:read")
            .Produces<PagedResponse<TenantMemberResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /v1/tenants/{id}/members/{memberId}
        group.MapGet("/{id}/members/{memberId}", GetMemberAsync)
            .WithName("GetMember")
            .RequireAuthorization("Permission:accounts:read")
            .Produces<TenantMemberResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /v1/tenants/{id}/members/{memberId}
        group.MapPatch("/{id}/members/{memberId}", UpdateMemberAsync)
            .WithName("UpdateMember")
            .RequireAuthorization("Permission:accounts:suspend")
            .Produces<TenantMemberResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // DELETE /v1/tenants/{id}/members/{memberId}
        group.MapDelete("/{id}/members/{memberId}", RemoveMemberAsync)
            .WithName("RemoveMember")
            .RequireAuthorization("Permission:accounts:remove")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // === Invites ===

        // GET /v1/tenants/{id}/invites
        group.MapGet("/{id}/invites", ListInvitesAsync)
            .WithName("ListInvites")
            .RequireAuthorization("Permission:accounts:invite")
            .Produces<PagedResponse<InviteResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /v1/tenants/{id}/members/invite
        group.MapPost("/{id}/members/invite", CreateInviteAsync)
            .WithName("CreateInvite")
            .RequireAuthorization("Permission:accounts:invite")
            .Produces<InviteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /v1/tenants/{id}/invites/{inviteId}
        group.MapDelete("/{id}/invites/{inviteId}", CancelInviteAsync)
            .WithName("CancelInvite")
            .RequireAuthorization("Permission:accounts:invite")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /v1/tenants/{id}/invites/{token}/accept - Public endpoint for accepting invites
        group.MapPost("/{id}/invites/{token}/accept", AcceptInviteAsync)
            .WithName("AcceptInvite")
            .Produces<TenantMemberResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // === Tenant CRUD ===

    private static async Task<IResult> CreateTenantAsync(
        [FromBody] CreateTenantRequest request,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateCreateTenantRequest(request);

        var userId = ClaimsHelper.GetUserId(user);
        var response = await tenantService.CreateTenantAsync(userId, request, ct);
        return Results.Created($"/v1/tenants/{response.Id}", response);
    }

    private static async Task<IResult> GetTenantAsync(
        string id,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var response = await tenantService.GetTenantAsync(tenantId, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateTenantAsync(
        string id,
        [FromBody] UpdateTenantRequest request,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var response = await tenantService.UpdateTenantAsync(tenantId, request, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransferOwnershipAsync(
        string id,
        [FromBody] TransferOwnershipRequest request,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var currentOwnerMemberId = ClaimsHelper.GetMemberId(user);
        var newOwnerMemberId = IdPrefix.DecodeMember(request.NewOwnerMemberId);

        await tenantService.TransferOwnershipAsync(tenantId, currentOwnerMemberId, newOwnerMemberId, ct);
        return Results.NoContent();
    }

    // === Members ===

    private static async Task<IResult> ListMembersAsync(
        string id,
        ITenantService tenantService,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var pagination = new PaginationRequest(cursor, limit);
        var response = await tenantService.ListMembersAsync(tenantId, pagination, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetMemberAsync(
        string id,
        string memberId,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var memberGuid = IdPrefix.DecodeMember(memberId);
        var response = await tenantService.GetMemberAsync(tenantId, memberGuid, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateMemberAsync(
        string id,
        string memberId,
        [FromBody] UpdateMemberRequest request,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var memberGuid = IdPrefix.DecodeMember(memberId);

        TenantMemberResponse response;
        if (request.Status == "suspended")
        {
            response = await tenantService.SuspendMemberAsync(tenantId, memberGuid, ct);
        }
        else if (request.Status == "active")
        {
            response = await tenantService.ActivateMemberAsync(tenantId, memberGuid, ct);
        }
        else
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure("status", "Status must be 'active' or 'suspended'")
            ]);
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> RemoveMemberAsync(
        string id,
        string memberId,
        ITenantService tenantService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var memberGuid = IdPrefix.DecodeMember(memberId);
        await tenantService.RemoveMemberAsync(tenantId, memberGuid, ct);
        return Results.NoContent();
    }

    // === Invites ===

    private static async Task<IResult> ListInvitesAsync(
        string id,
        IInviteService inviteService,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var pagination = new PaginationRequest(cursor, limit);
        var response = await inviteService.ListInvitesAsync(tenantId, pagination, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateInviteAsync(
        string id,
        [FromBody] CreateInviteRequest request,
        IInviteService inviteService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateCreateInviteRequest(request);

        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var invitedByMemberId = ClaimsHelper.GetMemberId(user);
        var response = await inviteService.CreateInviteAsync(tenantId, invitedByMemberId, request, ct);
        return Results.Created($"/v1/tenants/{id}/invites/{response.Id}", response);
    }

    private static async Task<IResult> CancelInviteAsync(
        string id,
        string inviteId,
        IInviteService inviteService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        ClaimsHelper.ValidateTenantAccess(user, tenantId);

        var inviteGuid = IdPrefix.DecodeInvite(inviteId);
        await inviteService.CancelInviteAsync(tenantId, inviteGuid, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AcceptInviteAsync(
        string id,
        string token,
        IInviteService inviteService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantId = IdPrefix.DecodeTenant(id);
        var userId = ClaimsHelper.GetUserId(user);

        var response = await inviteService.AcceptInviteAsync(tenantId, token, userId, ct);
        return Results.Ok(response);
    }

    // === Validation Helpers ===

    private static void ValidateCreateTenantRequest(CreateTenantRequest request)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();

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

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("slug", "Slug is required"));
        }
        else if (request.Slug.Length < 2)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("slug", "Slug must be at least 2 characters"));
        }
        else if (request.Slug.Length > 50)
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("slug", "Slug must not exceed 50 characters"));
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(request.Slug, @"^[a-z0-9-]+$"))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("slug", "Slug can only contain lowercase letters, numbers, and hyphens"));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void ValidateCreateInviteRequest(CreateInviteRequest request)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("email", "Email is required"));
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("email", "Invalid email format"));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
