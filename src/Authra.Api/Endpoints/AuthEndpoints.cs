using System.Security.Claims;
using Authra.Application.Auth;
using Authra.Application.Auth.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Infrastructure;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/auth")
            .WithTags("Auth");

        // POST /v1/auth/register
        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .RequireRateLimiting("register")
            .AllowAnonymous()
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /v1/auth/login
        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .RequireRateLimiting("login")
            .AllowAnonymous()
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<TenantSelectionRequired>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // POST /v1/auth/refresh
        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .AllowAnonymous()
            .Produces<RefreshResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // POST /v1/auth/logout
        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // POST /v1/auth/switch-tenant
        group.MapPost("/switch-tenant", SwitchTenantAsync)
            .WithName("SwitchTenant")
            .RequireAuthorization()
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // POST /v1/auth/password/reset-request
        group.MapPost("/password/reset-request", RequestPasswordResetAsync)
            .WithName("RequestPasswordReset")
            .RequireRateLimiting("password-reset")
            .AllowAnonymous()
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        // POST /v1/auth/password/reset
        group.MapPost("/password/reset", ResetPasswordAsync)
            .WithName("ResetPassword")
            .RequireRateLimiting("password-reset")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        IAuthService authService,
        IValidator<RegisterRequest> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var response = await authService.RegisterAsync(request, ct);
        return Results.Created($"/v1/users/{response.UserId}", response);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IAuthService authService,
        IValidator<LoginRequest> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await authService.LoginAsync(request, ct);

        return result.Match(
            loginResponse => Results.Ok(loginResponse),
            tenantSelection => Results.Ok(new
            {
                requiresTenantSelection = true,
                userId = tenantSelection.UserId,
                availableTenants = tenantSelection.AvailableTenants
            }));
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        IAuthService authService,
        IValidator<RefreshRequest> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var response = await authService.RefreshAsync(request, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest? request,
        IAuthService authService,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userId = GetUserIdFromClaims(user);
        var tenantId = GetTenantIdFromClaims(user);

        // Extract access token from Authorization header
        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        var accessToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authHeader["Bearer ".Length..].Trim()
            : null;

        await authService.LogoutAsync(
            userId,
            tenantId,
            request?.RefreshToken,
            accessToken,
            request?.LogoutAll ?? false,
            ct);

        return Results.NoContent();
    }

    private static async Task<IResult> SwitchTenantAsync(
        [FromBody] SwitchTenantRequest request,
        IAuthService authService,
        IValidator<SwitchTenantRequest> validator,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = GetUserIdFromClaims(user);
        var tenantId = ParsePrefixedId(request.TenantId, "tnt");

        var response = await authService.SwitchTenantAsync(userId, tenantId, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        [FromBody] PasswordResetRequestDto request,
        IAuthService authService,
        IValidator<PasswordResetRequestDto> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await authService.RequestPasswordResetAsync(request, ct);

        // Always return 202 to prevent email enumeration
        return Results.Accepted();
    }

    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] PasswordResetDto request,
        IAuthService authService,
        IValidator<PasswordResetDto> validator,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        await authService.ResetPasswordAsync(request, ct);
        return Results.NoContent();
    }

    private static Guid GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token");

        return Guid.Parse(sub);
    }

    private static Guid GetTenantIdFromClaims(ClaimsPrincipal user)
    {
        var tid = user.FindFirstValue("tid")
            ?? throw new UnauthorizedAccessException("Tenant ID not found in token");

        return Guid.Parse(tid);
    }

    private static Guid ParsePrefixedId(string prefixedId, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(prefixedId))
        {
            throw new ArgumentException($"Invalid {expectedPrefix} ID format");
        }

        var parts = prefixedId.Split('_', 2);
        if (parts.Length != 2 || parts[0] != expectedPrefix)
        {
            throw new ArgumentException($"Invalid {expectedPrefix} ID format");
        }

        if (!Guid.TryParse(parts[1], out var id))
        {
            throw new ArgumentException($"Invalid {expectedPrefix} ID format");
        }

        return id;
    }
}
