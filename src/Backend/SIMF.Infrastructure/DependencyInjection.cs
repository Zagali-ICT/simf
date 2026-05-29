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
using SIMF.Common.Options;

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
        // D-157 — two physically separate databases: SimfIdentityDb holds
        // auth + user-profile + identity-side audit; SimfAppDb holds the
        // event-data tables (Programme, Gates, Countries, Halls, …). Each
        // context owns its own migration history table inside its own
        // database. Cross-context references stay logical (no DB-level FK
        // across DBs in SQL Server); action logs in App carry snapshot
        // user-identity columns instead of a JOIN-back link.
        var identityConnection = configuration.GetConnectionString("SimfIdentityDb");
        if (string.IsNullOrWhiteSpace(identityConnection))
        {
            throw new InvalidOperationException(
                "Connection string 'SimfIdentityDb' is not configured.");
        }
        var appConnection = configuration.GetConnectionString("SimfAppDb");
        if (string.IsNullOrWhiteSpace(appConnection))
        {
            throw new InvalidOperationException(
                "Connection string 'SimfAppDb' is not configured.");
        }

        // D-109: scoped SaveChanges interceptor that writes a RowAudit row for
        // every INSERT/UPDATE/DELETE through either DbContext. Registered as
        // Scoped so each request gets the right IRequestContext (actor user id +
        // correlation id) injected.
        services.AddScoped<RowAuditingSaveChangesInterceptor>();

        // EnableRetryOnFailure covers the transient SQL errors of an Always On
        // failover (SIMF-SAD-001 §9).
        services.AddDbContext<SimfIdentityDbContext>((sp, options) =>
            options.UseSqlServer(identityConnection, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity");
                sql.EnableRetryOnFailure();
            }).AddInterceptors(sp.GetRequiredService<RowAuditingSaveChangesInterceptor>()));

        services.AddDbContext<SimfAppDbContext>((sp, options) =>
            options.UseSqlServer(appConnection, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory_App");
                sql.EnableRetryOnFailure();
            }).AddInterceptors(sp.GetRequiredService<RowAuditingSaveChangesInterceptor>()));

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
        services.AddScoped<IAdminProfileTypeCommandService, AdminProfileTypeCommandService>();
        // D-134 Sprint A — admin CRUD over SimfRole (existing schema, no migration).
        services.AddScoped<IAdminRoleService, AdminRoleService>();
        // D-134 Sprint A — read-only viewer over the OperationLog table.
        services.AddScoped<IAdminOperationLogService, AdminOperationLogService>();
        // D-134 Sprint A — read-only attendee roster (join over SimfUser +
        // UserProfile + ProfileType; no schema change).
        services.AddScoped<IAdminAttendeeService, AdminAttendeeService>();
        // D-134 Sprint B (D-135) — Themes admin CRUD (first new app-side table).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminThemeService,
            SIMF.Infrastructure.Programme.AdminThemeService>();
        // D-134 Sprint B (D-135) — Halls admin CRUD.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminHallService,
            SIMF.Infrastructure.Programme.AdminHallService>();
        // D-151 — Country admin lookup CRUD (under the lifted freeze).
        services.AddScoped<SIMF.Application.Common.Abstractions.IAdminCountryService,
            SIMF.Infrastructure.Common.AdminCountryService>();
        // D-153 — Speaker admin CRUD (enhanced shape: CountryId FK +
        // UserProfileId logical FK + bilingual rich text + consent + social).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSpeakerService,
            SIMF.Infrastructure.Programme.AdminSpeakerService>();
        // D-165 (gap doc G3) — Session admin CRUD: programme sessions tied
        // to a Hall + M-to-M Speakers + M-to-M Themes (PDF §2.9).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionService,
            SIMF.Infrastructure.Programme.AdminSessionService>();
        // D-166 (gap doc G4) — registration gate + archive visibility
        // singletons + the auto-close background worker (PDF §2.3, §2.4).
        services.AddScoped<SIMF.Application.Operations.Abstractions.IOperationsToggleService,
            SIMF.Infrastructure.Operations.OperationsToggleService>();
        services.AddHostedService<SIMF.Infrastructure.Operations.RegistrationGateAutoCloseWorker>();
        // D-148 — Gate Module: admin CRUD + operator surface + QR resolver +
        // gate-config cache + idempotency store + failure-rate circuit
        // (SIMF-API-GATES-001, SIMF-FDS-003 §5.6).
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IQrResolver,
            SIMF.Infrastructure.AccessControl.QrResolver>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IAdminGateService,
            SIMF.Infrastructure.AccessControl.AdminGateService>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IGateOperatorService,
            SIMF.Infrastructure.AccessControl.GateOperatorService>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IGateConfigCache,
            SIMF.Infrastructure.AccessControl.GateConfigCache>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IScanIdempotencyStore,
            SIMF.Infrastructure.AccessControl.ScanIdempotencyStore>();
        // The failure-rate circuit is singleton (in-memory state per process).
        services.AddSingleton<SIMF.Application.AccessControl.Abstractions.IGateFailureCircuit,
            SIMF.Infrastructure.AccessControl.GateFailureCircuit>();
        services.AddScoped<IAdminApprovalReadService, AdminApprovalReadService>();
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
