using Authra.Application.Auth;
using Authra.Application.Common.Interfaces;
using Authra.Application.Organizations;
using Authra.Application.Roles;
using Authra.Application.Tenants;
using Authra.Application.Users;
using Authra.Infrastructure.Persistence;
using Authra.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Configuration options with startup validation
        builder.Services.AddOptions<TokenOptions>()
            .BindConfiguration(TokenOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<PasswordHashingOptions>()
            .BindConfiguration(PasswordHashingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("Authra.Infrastructure");
                npgsqlOptions.EnableRetryOnFailure(3);
            }));

        builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Core services
        builder.Services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        builder.Services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<ITokenService, TokenService>();

        // Feature services
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ITenantService, TenantService>();
        builder.Services.AddScoped<IInviteService, InviteService>();
        builder.Services.AddScoped<IOrganizationService, OrganizationService>();
        builder.Services.AddScoped<IRoleService, RoleService>();

        // Email
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    }
}
