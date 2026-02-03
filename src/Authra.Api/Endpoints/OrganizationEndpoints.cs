using System.Security.Claims;
using Authra.Api.Infrastructure;
using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Organizations;
using Authra.Application.Organizations.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/tenants/{tenantId}/organizations")
            .WithTags("Organizations")
            .RequireAuthorization();

        // GET /v1/tenants/{tenantId}/organizations
        group.MapGet("/", ListOrganizationsAsync)
            .WithName("ListOrganizations")
            .RequireAuthorization("Permission:organizations:read")
            .Produces<PagedResponse<OrganizationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /v1/tenants/{tenantId}/organizations
        group.MapPost("/", CreateOrganizationAsync)
            .WithName("CreateOrganization")
            .RequireAuthorization("Permission:organizations:create")
            .Produces<OrganizationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /v1/tenants/{tenantId}/organizations/{orgId}
        group.MapGet("/{orgId}", GetOrganizationAsync)
            .WithName("GetOrganization")
            .RequireAuthorization("Permission:organizations:read")
            .Produces<OrganizationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /v1/tenants/{tenantId}/organizations/{orgId}
        group.MapPatch("/{orgId}", UpdateOrganizationAsync)
            .WithName("UpdateOrganization")
            .RequireAuthorization("Permission:organizations:update")
            .Produces<OrganizationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /v1/tenants/{tenantId}/organizations/{orgId}
        group.MapDelete("/{orgId}", DeleteOrganizationAsync)
            .WithName("DeleteOrganization")
            .RequireAuthorization("Permission:organizations:delete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // === Organization Members ===

        // GET /v1/tenants/{tenantId}/organizations/{orgId}/members
        group.MapGet("/{orgId}/members", ListOrganizationMembersAsync)
            .WithName("ListOrganizationMembers")
            .RequireAuthorization("Permission:organizations:members.read")
            .Produces<PagedResponse<OrganizationMemberResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /v1/tenants/{tenantId}/organizations/{orgId}/members
        group.MapPost("/{orgId}/members", AddOrganizationMemberAsync)
            .WithName("AddOrganizationMember")
            .RequireAuthorization("Permission:organizations:members.write")
            .Produces<OrganizationMemberResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /v1/tenants/{tenantId}/organizations/{orgId}/members/{memberId}
        group.MapDelete("/{orgId}/members/{memberId}", RemoveOrganizationMemberAsync)
            .WithName("RemoveOrganizationMember")
            .RequireAuthorization("Permission:organizations:members.write")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // === Organization CRUD ===

    private static async Task<IResult> ListOrganizationsAsync(
        string tenantId,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var pagination = new PaginationRequest(cursor, limit);
        var response = await organizationService.ListOrganizationsAsync(tenantGuid, pagination, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        string tenantId,
        [FromBody] CreateOrganizationRequest request,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateCreateOrganizationRequest(request);

        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var response = await organizationService.CreateOrganizationAsync(tenantGuid, request, ct);
        return Results.Created($"/v1/tenants/{tenantId}/organizations/{response.Id}", response);
    }

    private static async Task<IResult> GetOrganizationAsync(
        string tenantId,
        string orgId,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        var response = await organizationService.GetOrganizationAsync(tenantGuid, orgGuid, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        string tenantId,
        string orgId,
        [FromBody] UpdateOrganizationRequest request,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        var response = await organizationService.UpdateOrganizationAsync(tenantGuid, orgGuid, request, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteOrganizationAsync(
        string tenantId,
        string orgId,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        await organizationService.DeleteOrganizationAsync(tenantGuid, orgGuid, ct);
        return Results.NoContent();
    }

    // === Organization Members ===

    private static async Task<IResult> ListOrganizationMembersAsync(
        string tenantId,
        string orgId,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        var pagination = new PaginationRequest(cursor, limit);
        var response = await organizationService.ListMembersAsync(tenantGuid, orgGuid, pagination, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> AddOrganizationMemberAsync(
        string tenantId,
        string orgId,
        [FromBody] AddOrganizationMemberRequest request,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        ValidateAddMemberRequest(request);

        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        var memberGuid = IdPrefix.DecodeMember(request.MemberId);
        var response = await organizationService.AddMemberAsync(tenantGuid, orgGuid, memberGuid, ct);
        return Results.Created($"/v1/tenants/{tenantId}/organizations/{orgId}/members/{response.TenantMemberId}", response);
    }

    private static async Task<IResult> RemoveOrganizationMemberAsync(
        string tenantId,
        string orgId,
        string memberId,
        IOrganizationService organizationService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var tenantGuid = IdPrefix.DecodeTenant(tenantId);
        ClaimsHelper.ValidateTenantAccess(user, tenantGuid);

        var orgGuid = IdPrefix.DecodeOrganization(orgId);
        var memberGuid = IdPrefix.DecodeMember(memberId);
        await organizationService.RemoveMemberAsync(tenantGuid, orgGuid, memberGuid, ct);
        return Results.NoContent();
    }

    // === Validation Helpers ===

    private static void ValidateCreateOrganizationRequest(CreateOrganizationRequest request)
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

    private static void ValidateAddMemberRequest(AddOrganizationMemberRequest request)
    {
        var errors = new List<FluentValidation.Results.ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.MemberId))
        {
            errors.Add(new FluentValidation.Results.ValidationFailure("memberId", "Member ID is required"));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
