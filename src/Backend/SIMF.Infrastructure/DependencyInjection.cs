using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Persistence.Repositories;

namespace SIMF.Infrastructure;

/// <summary>
/// Registers the SIMF infrastructure services — the database contexts and the
/// repositories — with the dependency-injection container.
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

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountCodeRepository, AccountCodeRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        return services;
    }
}
