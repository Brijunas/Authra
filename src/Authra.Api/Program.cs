using Authra.Api.Endpoints;
using Authra.Api.Infrastructure;
using Authra.Application.Common.Interfaces;
using Authra.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Register services by layer
builder.AddApplication();
builder.AddInfrastructure();
builder.AddApi();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Authra API");
        options.WithTheme(ScalarTheme.DeepSpace);
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapTenantEndpoints();
app.MapOrganizationEndpoints();
app.MapRoleEndpoints();

app.MapGet("/.well-known/jwks.json", async ([FromServices] ITokenService tokenService, CancellationToken ct) =>
{
    var jwks = await ((TokenService)tokenService).GetJwksAsync(ct);
    return Results.Ok(jwks);
})
.WithName("GetJwks")
.WithTags("Auth")
.AllowAnonymous();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
