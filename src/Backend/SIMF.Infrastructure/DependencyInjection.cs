using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Email;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Persistence.Repositories;

namespace SIMF.Infrastructure;

/// <summary>
/// Registers the SIMF infrastructure services — the database contexts, ASP.NET
/// Core Identity, the repositories, the registration use case, the email
/// pipeline and the seeder — with the dependency-injection container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SimfDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'SimfDb' is not configured.");
        }

        // Both contexts target one physical database (decision C-1); each keeps
        // its own migration history table. EnableRetryOnFailure covers the
        // transient SQL errors of an Always On failover (SIMF-SAD-001 §9).
        services.AddDbContext<SimfIdentityDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity");
                sql.EnableRetryOnFailure();
            }));

        services.AddDbContext<SimfAppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_App");
                sql.EnableRetryOnFailure();
            }));

        // ASP.NET Core Identity — UserManager / RoleManager over the EF stores.
        // The §12.5 password policy is enforced by the request validators (one
        // place — SIMF-API-001 §12.5); Identity's own checks stay permissive.
        services.AddIdentityCore<SimfUser>(options =>
            {
                options.Password.RequiredLength = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<SimfRole>()
            .AddEntityFrameworkStores<SimfIdentityDbContext>();

        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SuperAdminOptions>(
            configuration.GetSection(SuperAdminOptions.SectionName));

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountCodeRepository, AccountCodeRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IdentitySeeder>();

        // Email — a singleton queue and sender drained by a background worker,
        // so a slow mail server never blocks a request (SIMF-SAD-001 A.2).
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailBackgroundService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
