using System.Security.Claims;
using Authra.Api.Infrastructure;
using Authra.Application.Users;
using Authra.Application.Users.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/me")
            .WithTags("Users")
            .RequireAuthorization();

        // GET /v1/me
        group.MapGet("/", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // PATCH /v1/me
        group.MapPatch("/", UpdateUsernameAsync)
            .WithName("UpdateUsername")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /v1/me/tenants
        group.MapGet("/tenants", GetUserTenantsAsync)
            .WithName("GetUserTenants")
            .Produces<IReadOnlyList<UserTenantResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetCurrentUserAsync(
        IUserService userService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = ClaimsHelper.GetUserId(user);
        var response = await userService.GetCurrentUserAsync(userId, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateUsernameAsync(
        [FromBody] UpdateUsernameRequest request,
        IUserService userService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure("username", "Username is required")
            ]);
        }

        if (request.Username.Length < 3)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure("username", "Username must be at least 3 characters")
            ]);
        }

        var userId = ClaimsHelper.GetUserId(user);
        var response = await userService.UpdateUsernameAsync(userId, request.Username, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetUserTenantsAsync(
        IUserService userService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = ClaimsHelper.GetUserId(user);
        var response = await userService.GetUserTenantsAsync(userId, ct);
        return Results.Ok(response);
    }
}
