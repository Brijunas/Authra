using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Authra.Api.Endpoints;
using Authra.Api.Infrastructure;
using Authra.Application.Auth;
using Authra.Application.Auth.Validators;
using Authra.Application.Common.Interfaces;
using Authra.Application.Organizations;
using Authra.Application.Roles;
using Authra.Application.Tenants;
using Authra.Application.Users;
using Authra.Infrastructure.Persistence;
using Authra.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// Add configuration options
builder.Services.Configure<TokenOptions>(builder.Configuration.GetSection(TokenOptions.SectionName));
builder.Services.Configure<PasswordHashingOptions>(builder.Configuration.GetSection(PasswordHashingOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));

// Add database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly("Authra.Infrastructure");
        npgsqlOptions.EnableRetryOnFailure(3);
    }));

// Add services
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IInviteService, InviteService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IRoleService, RoleService>();

// Add email service (Mailpit for dev, SMTP for prod)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// Add authentication with custom token validation
var tokenOptions = builder.Configuration.GetSection(TokenOptions.SectionName).Get<TokenOptions>() ?? new TokenOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Disable inbound claim type mapping to preserve original claim names (e.g., "tid" stays "tid")
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = false, // We validate keys manually in OnTokenValidated
            SignatureValidator = (token, parameters) =>
            {
                // Skip signature validation here; we'll validate in OnTokenValidated
                // This is a workaround for async key loading from DB
                var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
                return handler.ReadJsonWebToken(token);
            }
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                // Validate token signature and blacklist using TokenService
                var tokenService = context.HttpContext.RequestServices.GetRequiredService<ITokenService>();
                var rawToken = context.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");

                if (string.IsNullOrEmpty(rawToken))
                {
                    context.Fail("No token provided");
                    return;
                }

                var claims = await tokenService.ValidateAccessTokenAsync(rawToken);
                if (claims == null)
                {
                    context.Fail("Token validation failed");
                    return;
                }

                // Token is valid - no additional action needed as principal is already set
            }
        };
    });

// Add authorization with permission policies
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login rate limit: 5 per 15 minutes per IP
    options.AddPolicy("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3
            }));

    // Register rate limit: 3 per hour per IP
    options.AddPolicy("register", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                SegmentsPerWindow = 4
            }));

    // Password reset rate limit: 3 per 15 minutes per IP
    options.AddPolicy("password-reset", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3
            }));

    // Default rate limit: 1000 per minute per user/IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var user = context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(user, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 1000,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6
        });
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add health checks
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql");
}
else
{
    builder.Services.AddHealthChecks();
}

// Add OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "Authra API";
        document.Info.Version = "v1";
        document.Info.Description = "Multi-tenant identity and authentication system";
        return Task.CompletedTask;
    });
});

// Add problem details
builder.Services.AddProblemDetails();

// Add global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

// Map health check endpoint
app.MapHealthChecks("/health");

// Map auth endpoints
app.MapAuthEndpoints();

// Map user endpoints
app.MapUserEndpoints();

// Map tenant endpoints
app.MapTenantEndpoints();

// Map organization endpoints
app.MapOrganizationEndpoints();

// Map role endpoints
app.MapRoleEndpoints();

// Map JWKS endpoint
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
