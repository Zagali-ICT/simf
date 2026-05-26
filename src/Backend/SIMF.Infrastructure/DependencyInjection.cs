using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Notifications;
using SIMF.Infrastructure.Notifications;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Logs;
using SIMF.Infrastructure.Logs;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Auditing;
using SIMF.Infrastructure.Email;
using SIMF.Infrastructure.Excel;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Persistence.Repositories;
using SIMF.Infrastructure.Storage;

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
        // Identity enforces the SIMF-API-001 §12.5 baseline (length and a digit)
        // so every credential path is covered, including the seeder; the request
        // validators add the remaining rules (a letter, not equal to the email)
        // with field-level messages.
        services.AddIdentityCore<SimfUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // Account lockout — the brute-force defence (SIMF-FDS-001 A.1).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<SimfRole>()
            .AddEntityFrameworkStores<SimfIdentityDbContext>();

        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SuperAdminOptions>(
            configuration.GetSection(SuperAdminOptions.SectionName));
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));
        // R1 — D-074: typed Storage settings; replaces four scattered
        // IConfiguration["Storage:..."] reads across FilesystemAvatarStorage,
        // EncryptedUserIdDocumentStorage, LogFileService, and Program.cs.
        // ValidateOnStart fires at host build time, so a missing AvatarBase
        // surfaces as an OptionsValidationException at boot rather than on
        // first request — same fail-fast posture as the pre-R1 explicit
        // Program.cs gate.
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AvatarBase),
                "Storage:AvatarBase must be configured (filesystem path for user avatars).")
            .ValidateOnStart();

        // R3 — D-076: Application code asks for SimfUser through this
        // repository abstraction; UserManager stays in Infrastructure.
        // R3.5 — D-094: the 22-method aggregate is split into five role-
        // cohesive sub-interfaces. One scoped UserAccountRepository instance
        // backs all six registrations (the aggregate + the five
        // sub-interfaces) so the change-tracker scope and per-request state
        // stay shared — same pattern R2 (D-075) used for AdminAccountService.
        services.AddScoped<UserAccountRepository>();
        services.AddScoped<IUserAccountRepository>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserAccountStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserCredentialStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserLockoutTracker>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserRoleStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserTwoFactorStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountCodeRepository, AccountCodeRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ISecondFactorTokenRepository, SecondFactorTokenRepository>();
        services.AddScoped<ITotpRecoveryCodeRepository, TotpRecoveryCodeRepository>();
        // R4 — D-095: persistence seams for the services that moved from
        // Infrastructure → Application. The services no longer inject
        // SimfIdentityDbContext directly.
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITotpEnrollmentService, TotpEnrollmentService>();
        services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        services.AddScoped<IAccountService, AccountService>();
        // R2 — D-075: AdminAccountService implements the five focused
        // interfaces split out of the pre-R2 IAdminAccountService
        // (Architecture SEV-1.2). One scoped instance backs all five
        // registrations so the surrounding shared state (audit log,
        // db context, etc.) stays per-request.
        services.AddScoped<AdminAccountService>();
        services.AddScoped<IAdminTwoFactorService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserApprovalService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserProvisioningService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserBulkService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminProfileTypeQueryService, AdminProfileTypeQueryService>();
        services.AddScoped<IQrIdMinter, QrIdMinter>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IInterestService, InterestService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IUserExcelService, ClosedXmlUserExcelService>();
        services.AddSingleton<IAvatarStorage, FilesystemAvatarStorage>();
        services.AddSingleton<IUserIdDocumentStorage, EncryptedUserIdDocumentStorage>();
        services.AddSingleton<ILogFileService, LogFileService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITotpVerifier, TotpVerifier>();
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
