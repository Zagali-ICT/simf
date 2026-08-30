using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Notifications;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Logs;
using SIMF.Infrastructure.Logs;
using SIMF.Infrastructure.Operations;
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
    /// <summary>Refuses to start in Production when the AI
    /// prompt-hash HMAC secret was never configured. The dev-fallback key is
    /// derived from a public constant string, so a dev-fallback hash leaking
    /// into a production audit trail is trivially recomputable. Call from the
    /// host after <see cref="AddInfrastructure"/>, which installs the key.</summary>
    public static void EnsureAiPromptHashSecretConfigured(bool isProduction)
    {
        if (isProduction && SIMF.Infrastructure.Ai.AiAuditDetail.IsHmacKeyDevFallback)
        {
            throw new InvalidOperationException(
                "Ai:PromptHash:Secret must be configured in Production — the "
                + "dev-fallback HMAC key is publicly derivable. Set "
                + "SIMF_API_Ai__PromptHash__Secret before starting.");
        }
    }

    /// <summary>A2-10 (security) — refuse to start in Production when the PII
    /// encryption key (<c>Storage:UserIdDocumentEncryptionKey</c>, reused for the
    /// UserProfile identifier columns) is missing or not a valid 32-byte base64
    /// key. Without it, every write of national ID / Iqama / passport / mobile
    /// would throw at runtime. Call from the host after the app is built.</summary>
    public static void EnsurePiiEncryptionConfigured(bool isProduction, IServiceProvider services)
    {
        if (isProduction
            && !services.GetRequiredService<SIMF.Application.Abstractions.IPiiEncryptor>().IsKeyConfigured)
        {
            throw new InvalidOperationException(
                "Storage:UserIdDocumentEncryptionKey must be a valid base64 32-byte key in "
                + "Production — it encrypts the UserProfile PII columns at rest (NCA A2-10). "
                + "Set SIMF_API_Storage__UserIdDocumentEncryptionKey before starting.");
        }
    }

    /// <summary>Refuse to start when the centralized file-store key ring cannot
    /// work: the KEK (<c>FileStorage:EncryptionKey</c>) is missing in Production,
    /// or a rotation was configured with the retiring key sitting on the ACTIVE
    /// key's version. The <c>AesGcmEnvelopeCipher</c> fail-fasts on both, but a
    /// cipher is not constructed until something is uploaded, so without this
    /// guard the operator learns of a broken key ring from a failed upload hours
    /// after the deploy rather than from a refused start. Call from the host after
    /// the app is built.</summary>
    public static void EnsureFileStorageEncryptionConfigured(bool isProduction, IServiceProvider services)
    {
        var fileStorage = services
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<SIMF.Common.Options.FileStorageOptions>>()
            .Value;

        if (isProduction && string.IsNullOrWhiteSpace(fileStorage.EncryptionKey))
        {
            throw new InvalidOperationException(
                "FileStorage:EncryptionKey must be a base64 32-byte AES key in Production — "
                + "it is the KEK for the centralized file store. "
                + "Set SIMF_API_FileStorage__EncryptionKey before starting.");
        }

        // Unlike the missing key above, this one is asserted in EVERY environment.
        // An absent key is the normal state of a developer machine; a rotation whose
        // two keys claim the SAME version is wrong everywhere. The key ring is a
        // dictionary keyed by version, so the collision leaves ONE entry holding the
        // retiring key under the number the new key was meant to occupy, and every
        // file written afterwards is sealed under the very key the rotation existed
        // to retire while carrying the new version in its header.
        if (!string.IsNullOrWhiteSpace(fileStorage.PreviousEncryptionKey)
            && fileStorage.PreviousKekVersion == fileStorage.KekVersion)
        {
            throw new InvalidOperationException(
                "FileStorage:PreviousKekVersion must differ from FileStorage:KekVersion; "
                + $"both are {fileStorage.KekVersion}. A rotation supplies the retiring "
                + "key under its OWN version and bumps the active one: set "
                + "SIMF_API_FileStorage__KekVersion to the new version and leave "
                + "SIMF_API_FileStorage__PreviousKekVersion on the outgoing one.");
        }
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Two physically separate databases: SimfIdentityDb holds
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

        // Scoped SaveChanges interceptor that writes a RowAudit row for
        // every INSERT/UPDATE/DELETE through either DbContext. Registered as
        // Scoped so each request gets the right IRequestContext (actor user id +
        // correlation id) injected.
        services.AddScoped<RowAuditingSaveChangesInterceptor>();
        // Stamps CreatedBy/CreatedAt + UpdatedBy/UpdatedAt on every audited App
        // entity (BaseAuditEntity) from the signed-in actor. Scoped so it sees
        // the per-request IRequestContext. Registered BEFORE the row-audit
        // interceptor below so the stamped values land in the audit trail.
        services.AddScoped<AuditStampingSaveChangesInterceptor>();
        // Stamps the open edition year onto every new attendee record. Singleton
        // cache behind it, because the year changes about once a year and is read
        // on nearly every attendee write and every gate scan.
        services.AddSingleton<
            SIMF.Infrastructure.Editions.IEventEditionCache,
            SIMF.Infrastructure.Editions.EventEditionCache>();
        services.AddScoped<SIMF.Infrastructure.Editions.EditionStampingSaveChangesInterceptor>();
        services.AddScoped<
            SIMF.Application.Editions.Abstractions.IEventEditionService,
            SIMF.Infrastructure.Editions.EventEditionService>();

        // EnableRetryOnFailure covers the transient SQL errors of an Always On
        // failover.
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
            }).AddInterceptors(
                sp.GetRequiredService<AuditStampingSaveChangesInterceptor>(),
                sp.GetRequiredService<
                    SIMF.Infrastructure.Editions.EditionStampingSaveChangesInterceptor>(),
                sp.GetRequiredService<RowAuditingSaveChangesInterceptor>()));

        // ASP.NET Core Identity — UserManager / RoleManager over the EF stores.
        // The built-in validator enforces the length baseline;
        // the content rules (NCA A7-29 — complexity classes, no repeats/sequences,
        // no common passwords, not equal to the identifier) live in the central
        // SimfPasswordValidator so every credential path — sign-up, admin-create,
        // reset, change, the seeder — is covered identically, and the request
        // validators surface the same rules with bilingual field-level messages.
        services.AddIdentityCore<SimfUser>(options =>
            {
                options.Password.RequiredLength = 8;
                // The built-in character requirements are owned by
                // SimfPasswordValidator (so the policy is defined once); leave the
                // built-in flags off to avoid duplicate generic error entries.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // Account lockout — the brute-force defence.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<SimfRole>()
            .AddEntityFrameworkStores<SimfIdentityDbContext>()
            // NCA A7-21 — central password validator (see SimfPasswordValidator).
            .AddPasswordValidator<SimfPasswordValidator>();

        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SuperAdminOptions>(
            configuration.GetSection(SuperAdminOptions.SectionName));
        // The demo user-account seed shares one password sourced here.
        services.Configure<DemoSeedOptions>(
            configuration.GetSection(DemoSeedOptions.SectionName));
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));
        // Ops override — the misremembered Session:TimeoutHours (env
        // SIMF_API_Session__TimeoutHours) lengthens the short-lived access token
        // beyond the NCA-default 5 minutes at runtime. Absent → the NCA default
        // stands; set → clamped to the 24h absolute session cap so it
        // can never exceed it. Kept OUT of the committed set-env-api template so
        // the shipped deploy posture stays NCA-compliant; an operator opts in.
        services.PostConfigure<JwtOptions>(options =>
            options.AccessTokenMinutes = JwtOptions.ResolveAccessTokenMinutes(
                options.AccessTokenMinutes,
                configuration.GetValue<int>("Session:TimeoutHours", 0),
                options.SessionLifetimeHours));
        // A7-13 (NCA) — credential-lifecycle settings (password max age).
        services.Configure<IdentityLifecycleOptions>(
            configuration.GetSection(IdentityLifecycleOptions.SectionName));
        // A6-18 (NCA) — upload malware-scanning settings.
        services.Configure<UploadScanningOptions>(
            configuration.GetSection(UploadScanningOptions.SectionName));
        // Biometric device-key enrolment step-up toggle (default on).
        services.Configure<DeviceKeyOptions>(
            configuration.GetSection(DeviceKeyOptions.SectionName));
        // The speaker email-link base URL + TTL.
        services.Configure<MeetingLinksOptions>(
            configuration.GetSection(MeetingLinksOptions.SectionName));
        // Typed Storage settings. What is left of this section is
        // the PII encryption key and the log directory; the avatar, VIP-photo
        // and ID-document paths went with the move to the unified
        // StoredFile store. The AvatarBase boot gate went with them: it made
        // the API refuse to start without a path nothing would ever open. The
        // gate that still matters is FileStorage:EncryptionKey, enforced by
        // the file-cipher's own boot check.
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName));

        // The standby walk-in capability. Bound WITHOUT ValidateOnStart
        // and with no required values: every switch defaults to off, so an
        // absent section is the normal, correct state and must never block boot.
        // Consumers take IOptionsMonitor so arming it in appsettings /
        // set-env-* takes effect without a restart.
        services.Configure<WalkInModeOptions>(
            configuration.GetSection(WalkInModeOptions.SectionName));

        // Application code asks for SimfUser through this
        // repository abstraction; UserManager stays in Infrastructure.
        // The 22-method aggregate is split into five role-
        // cohesive sub-interfaces. One scoped UserAccountRepository instance
        // backs all six registrations (the aggregate + the five
        // sub-interfaces) so the change-tracker scope and per-request state
        // stay shared — the same pattern AdminAccountService uses.
        services.AddScoped<UserAccountRepository>();
        services.AddScoped<IUserAccountRepository>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserAccountStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserCredentialStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserLockoutTracker>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserRoleStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IUserTwoFactorStore>(sp => sp.GetRequiredService<UserAccountRepository>());
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountCodeRepository, AccountCodeRepository>();
        // A7-20 (NCA) — retired-password-hash store for reuse prevention.
        services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
        // A1-19 (NCA) — dormant-account auto-disable (driven by the daily sweep host).
        services.AddScoped<IDormantAccountService, DormantAccountService>();
        // A4 (NCA data-minimisation) — retention purge of dead security artifacts
        // (driven by the daily RetentionSweepWorker host).
        services.AddScoped<IRetentionPurgeService, RetentionPurgeService>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        // Resolves Identity-owned user attributes for App-side services across
        // the DB boundary — a second query, never a cross-database JOIN.
        services.AddScoped<IIdentityUserDirectory, IdentityUserDirectory>();
        services.AddScoped<ISecondFactorTokenRepository, SecondFactorTokenRepository>();
        services.AddScoped<ITotpRecoveryCodeRepository, TotpRecoveryCodeRepository>();
        // Persistence seams for the services that moved from
        // Infrastructure → Application. The services no longer inject
        // SimfIdentityDbContext directly.
        services.AddScoped<INotificationRepository, NotificationRepository>();
        // InterestService moved to Application; its EF query
        // shapes live behind IInterestRepository (over SimfAppDbContext).
        services.AddScoped<IInterestRepository, InterestRepository>();
        // UserProfileService moved to Application; it spans both
        // DBs (profile + lookups on App, account reads + transactional save on
        // Identity) behind IUserProfileRepository.
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        // Resolves a user's permission codes from their roles for
        // the `perm` claim baked into the JWT (Administrator → wildcard).
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        // The one place a session is minted. The
        // password sign-in, the badge-QR sign-in (which delegates to it) and the
        // device-key ceremony all resolve this, so the claim set and the
        // absolute session cap cannot drift between entry points.
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IPasswordService, PasswordService>();
        // Badge-QR sign-in / activation.
        services.AddScoped<IBadgeAuthService, BadgeAuthService>();
        services.AddScoped<ITotpEnrollmentService, TotpEnrollmentService>();
        services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        // AdminAccountService implements the five focused
        // interfaces split out of the original aggregate
        // IAdminAccountService. One scoped instance backs all five
        // registrations so the surrounding shared state (audit log,
        // db context, etc.) stays per-request.
        services.AddScoped<AdminAccountService>();
        services.AddScoped<IAdminTwoFactorService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserApprovalService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserProvisioningService>(sp => sp.GetRequiredService<AdminAccountService>());
        services.AddScoped<IAdminUserBulkService>(sp => sp.GetRequiredService<AdminAccountService>());
        // The offline badge desk's reconciliation upload. Its own class
        // rather than another AdminAccountService facet: it owns no write path,
        // only the sequence-to-QR-id mapping and per-item error isolation.
        services.AddScoped<IOfflineBadgeUploadService, OfflineBadgeUploadService>();
        services.AddScoped<IAdminProfileTypeQueryService, AdminProfileTypeQueryService>();
        services.AddScoped<IAdminProfileTypeCommandService, AdminProfileTypeCommandService>();
        // Admin CRUD over SimfRole (existing schema, no migration).
        services.AddScoped<IAdminRoleService, AdminRoleService>();
        // Read-only viewer over the OperationLog table.
        services.AddScoped<IAdminOperationLogService, AdminOperationLogService>();
        // Read-only attendee roster (join over SimfUser +
        // UserProfile + ProfileType; no schema change).
        services.AddScoped<IAdminAttendeeService, AdminAttendeeService>();
        // Themes admin CRUD (first new app-side table).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminThemeService,
            SIMF.Infrastructure.Programme.AdminThemeService>();
        // Halls admin CRUD.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminHallService,
            SIMF.Infrastructure.Programme.AdminHallService>();
        // Country admin lookup CRUD (under the lifted freeze).
        services.AddScoped<SIMF.Application.Common.Abstractions.IAdminCountryService,
            SIMF.Infrastructure.Common.AdminCountryService>();
        // Speaker admin CRUD (enhanced shape: CountryId FK +
        // UserProfileId logical FK + bilingual rich text + consent + social).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSpeakerService,
            SIMF.Infrastructure.Programme.AdminSpeakerService>();
        // Speaker presentation-file management + storage.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSpeakerPresentationService,
            SIMF.Infrastructure.Programme.AdminSpeakerPresentationService>();
        // System Configuration settings store.
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IAdminSystemSettingService,
            SIMF.Infrastructure.Configuration.AdminSystemSettingService>();
        // Public read-path over the whitelisted site-settings keys
        // (registration welcome message + social links).
        services.AddScoped<SIMF.Application.Configuration.Abstractions.ISiteSettingsService,
            SIMF.Infrastructure.Configuration.SiteSettingsService>();
        // Public read-path over the whitelisted app-update version-policy
        // keys (per-platform min/latest app version + store URL).
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IAppVersionPolicyService,
            SIMF.Infrastructure.Configuration.AppVersionPolicyService>();
        // The two walk-in desk modes: deployment configuration with a CP
        // override on top. Every runtime reader of QuickRegisterActive /
        // AutoApproveActive goes through this rather than the options monitor,
        // or the admin's toggle would be silently ignored.
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IWalkInModeSettings,
            SIMF.Infrastructure.Configuration.WalkInModeSettingsService>();
        // The singleton Organization / About profile: cached public read +
        // admin full-document upsert (the edition-generic forum config).
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IOrganizationProfileReadService,
            SIMF.Infrastructure.Configuration.OrganizationProfileReadService>();
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IOrganizationProfileAdminService,
            SIMF.Infrastructure.Configuration.OrganizationProfileAdminService>();
        // The CP-uploaded hero background video, served from our own API
        // (streamed store + public Range serve) so the Flutter home hero plays it on
        // Android, where a clipped YouTube WebView cannot render into the band.
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IOrganizationHeroVideoService,
            SIMF.Infrastructure.Configuration.OrganizationHeroVideoService>();
        services.Configure<SIMF.Infrastructure.Configuration.OrganizationHeroVideoOptions>(
            configuration.GetSection(SIMF.Infrastructure.Configuration.OrganizationHeroVideoOptions.SectionName));
        // 2D venue map (admin CRUD + public read).
        services.AddScoped<SIMF.Application.Venue.Abstractions.IVenueMapService,
            SIMF.Infrastructure.Venue.VenueMapService>();
        // Session admin CRUD: programme sessions tied
        // to a Hall + M-to-M Speakers + M-to-M Themes.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionService,
            SIMF.Infrastructure.Programme.AdminSessionService>();
        // AI session-summary / محضر committee desk (drafts via
        // the central IAiService seam; publishes for the app to read).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionSummaryService,
            SIMF.Infrastructure.Programme.AdminSessionSummaryService>();
        // Attendee-facing hall arrival/departure via GPS geofence.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IHallAttendanceService,
            SIMF.Infrastructure.Programme.HallAttendanceService>();
        // Movement / dwell / route tracking — the periodic
        // device-position capture path plus its two aggregate reads. Inert until a
        // hall is given a geofence boundary from the CP.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IMovementTrackingService,
            SIMF.Infrastructure.Programme.MovementTrackingService>();
        // Recordings now live in the unified StoredFile store;
        // SessionRecordingStorageOptions is kept only for the upload endpoint's
        // MaxUploadBytes ceiling (the bespoke recording store is gone).
        services.Configure<SIMF.Infrastructure.Programme.SessionRecordingStorageOptions>(
            configuration.GetSection(SIMF.Infrastructure.Programme.SessionRecordingStorageOptions.SectionName));
        // Registration gate + archive visibility
        // singletons + the auto-close background worker.
        services.AddScoped<SIMF.Application.Operations.Abstractions.IOperationsToggleService,
            SIMF.Infrastructure.Operations.OperationsToggleService>();
        // In-process heartbeat registry the hosted workers report to, so the CP
        // services monitor and /health can tell which workers are up. Singleton,
        // shared by the API host and every worker (no schema, no cross-process).
        services.AddSingleton<SIMF.Application.Operations.IWorkerHeartbeatRegistry,
            SIMF.Infrastructure.Operations.WorkerHeartbeatRegistry>();
        // Elects one API instance to run the workers below. Every one of them is
        // a database scan or a status-claimed queue, so four instances each
        // running their own copy would duplicate the send and race the once-only
        // guards. Registered before them so the lease starts first; the gate does
        // not block startup either way. EmailBackgroundService is deliberately
        // NOT leased - see AddLeasedHostedService.
        services.AddWorkerLease();
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.RegistrationGateAutoCloseWorker>();
        // Automated "session starting soon" reminder worker.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.SessionReminderWorker>();
        // 15-min "meeting starting soon" reminder (email + app) for
        // confirmed speaker + delegation meetings.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.MeetingReminderWorker>();
        // Reverts a stuck AwaitingSpeaker speaker meeting request to Pending once
        // its 72h double-opt-in tokens expire (no re-send ever came); frees the held slot.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.MeetingAwaitingSpeakerExpiryWorker>();
        // Releases seats reserved by no-shows (no check-in) 3 minutes
        // before the session starts, freeing capacity for others.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.ReservationNoShowReleaseWorker>();
        // "The session started and you have not arrived": nudges holders of
        // an active reservation with no HallAttendance row, a few minutes after the
        // session starts. Sibling of the no-show release worker, which frees the
        // seat but notifies nobody.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.SessionNotAttendedReminderWorker>();
        // Pushes a "you match this attendee" invitation for every candidate
        // the recommendation engine scores at or above the 80% threshold.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.MatchRecommendationPushWorker>();
        // End-of-session "please rate this session" prompt worker.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.SessionRatingPromptWorker>();
        // End-of-day + end-of-programme rating prompt worker.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.ProgrammeRatingPromptWorker>();
        // Chain reconciliation — closes open hall-attendance rows whose
        // session has ended (In-only hall-door gates never emit a departure).
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.HallAttendanceCloseoutWorker>();
        // Control Panel "Announcements" desk — fans out manual admin notification
        // broadcasts (in-app row + email per recipient) to a session's attendees or
        // a broad audience, paced against the bounded email queue.
        services.AddLeasedHostedService<SIMF.Infrastructure.Operations.NotificationBroadcastWorker>();
        // Public-relations team: invitation CRUD +
        // VIP list + bulk-notify dispatcher.
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminInvitationService,
            SIMF.Infrastructure.PublicRelations.AdminInvitationService>();
        // Session-question moderation: public submit
        // + per-session moderator queue + admin assignment of moderators
        // (distinct from MobileAppRole.Moderator).
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionQuestionService,
            SIMF.Infrastructure.SessionQuestions.SessionQuestionService>();
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionModerationService,
            SIMF.Infrastructure.SessionQuestions.SessionModerationService>();
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.IAdminSessionModeratorService,
            SIMF.Infrastructure.SessionQuestions.AdminSessionModeratorService>();
        // Scientific-Committee central Q&A queue (stage 2).
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionQuestionCommitteeService,
            SIMF.Infrastructure.SessionQuestions.SessionQuestionCommitteeService>();
        // "Meet People Like You" interest-intersection
        // ranker. Read-only service over UserProfile.Interests.
        services.AddScoped<SIMF.Application.Recommendations.Abstractions.IRecommendationService,
            SIMF.Infrastructure.Recommendations.RecommendationService>();
        // Face ID / Touch ID biometric sign-in via
        // ECDSA P-256 device key.
        services.AddScoped<SIMF.Application.IdentityAccess.Abstractions.IDeviceKeyService,
            SIMF.Infrastructure.IdentityAccess.DeviceKeyService>();
        // Dynamic content CMS: admin CRUD over
        // ContentBlock + Banner, plus the public read
        // surface for the Flutter app + Website.
        services.AddScoped<SIMF.Application.Cms.Abstractions.IAdminCmsService,
            SIMF.Infrastructure.Cms.AdminCmsService>();
        services.AddScoped<SIMF.Application.Cms.Abstractions.IPublicCmsService,
            SIMF.Infrastructure.Cms.PublicCmsService>();
        // Attendee meeting requests to a speaker.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.ISpeakerMeetingRequestService,
            SIMF.Infrastructure.MeetingRequests.SpeakerMeetingRequestService>();
        // Speaker availability windows + free-slot derivation.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.ISpeakerAvailabilityService,
            SIMF.Infrastructure.MeetingRequests.SpeakerAvailabilityService>();
        // Bi-Meeting rework — delegation availability windows + free-slot derivation.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IDelegationAvailabilityService,
            SIMF.Infrastructure.MeetingRequests.DelegationAvailabilityService>();
        // Hall availability windows (hall time
        // for business meetings) + free-slot derivation.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IHallAvailabilityService,
            SIMF.Infrastructure.MeetingRequests.HallAvailabilityService>();
        // Speaker double-opt-in action tokens.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IMeetingActionTokenService,
            SIMF.Infrastructure.MeetingRequests.MeetingActionTokenService>();
        // Delegation↔delegation meeting requests.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IDelegationMeetingRequestService,
            SIMF.Infrastructure.MeetingRequests.DelegationMeetingRequestService>();
        // The unified "My requests" (الطلبات) feed, which supersedes the
        // old read-only My-meetings feed, plus the two new standalone request
        // types (participation-document + badge-update).
        services.AddScoped<SIMF.Application.Requests.Abstractions.IMyRequestsService,
            SIMF.Infrastructure.Requests.MyRequestsService>();
        services.AddScoped<SIMF.Application.Requests.Abstractions.IParticipationDocumentRequestService,
            SIMF.Infrastructure.Requests.ParticipationDocumentRequestService>();
        services.AddScoped<SIMF.Application.Requests.Abstractions.IBadgeUpdateRequestService,
            SIMF.Infrastructure.Requests.BadgeUpdateRequestService>();
        // Per-session seat reservations (visitor self-pick + random +
        // admin row blocks).
        services.AddScoped<SIMF.Application.SeatReservations.Abstractions.ISeatReservationService,
            SIMF.Infrastructure.SeatReservations.SeatReservationService>();
        // Flexible hall config + admin-arranged B2B/B2C
        // business meetings (meeting tables, hall allocations, meetings).
        services.AddScoped<SIMF.Application.BusinessMeetings.Abstractions.IBusinessMeetingService,
            SIMF.Infrastructure.BusinessMeetings.BusinessMeetingService>();
        // The app's My-Area dashboard (held bookings + accepted
        // speaker meetings + confirmed business meetings + identity card).
        services.AddScoped<SIMF.Application.MyArea.IMyAreaService,
            SIMF.Infrastructure.MyArea.MyAreaService>();
        // The app's five accessibility choices as
        // account preferences (GET / PUT /app/account/preferences), so they follow
        // the user to a second device and survive a reinstall.
        services.AddScoped<SIMF.Application.Preferences.IAccountPreferencesService,
            SIMF.Infrastructure.Preferences.AccountPreferencesService>();
        // Event modules (freeze lift): programme/speaker public reads,
        // news, media + media-partners, booths, sponsors, archive, ratings.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IPublicSpeakerService,
            SIMF.Infrastructure.Programme.PublicSpeakerService>();
        services.AddScoped<SIMF.Application.Programme.Abstractions.IProgrammeSessionService,
            SIMF.Infrastructure.Programme.ProgrammeSessionService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IPublicNewsService,
            SIMF.Infrastructure.PublicRelations.PublicNewsService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminNewsService,
            SIMF.Infrastructure.PublicRelations.AdminNewsService>();
        // FAQ management (two-level group → entry).
        services.AddScoped<SIMF.Application.Faq.Abstractions.IAdminFaqService,
            SIMF.Infrastructure.Faq.AdminFaqService>();
        // Public, anonymous FAQ read for the app accordion.
        services.AddScoped<SIMF.Application.Faq.Abstractions.IPublicFaqService,
            SIMF.Infrastructure.Faq.PublicFaqService>();
        // Contact-us inquiries — public submit + CP inbox.
        services.AddScoped<SIMF.Application.Support.Abstractions.IContactInquiryService,
            SIMF.Infrastructure.Support.ContactInquiryService>();
        // Session favourites (المفضلة) — heart toggle on summaries + my-sessions.
        services.AddScoped<SIMF.Application.Programme.Abstractions.ISessionFavouriteService,
            SIMF.Infrastructure.Programme.SessionFavouriteService>();
        // Public read + download of speaker presentations.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IPublicSpeakerPresentationService,
            SIMF.Infrastructure.Programme.PublicSpeakerPresentationService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IPublicMediaPartnerService,
            SIMF.Infrastructure.PublicRelations.PublicMediaPartnerService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminMediaPartnerService,
            SIMF.Infrastructure.PublicRelations.AdminMediaPartnerService>();
        services.AddScoped<SIMF.Application.Media.Abstractions.IPublicMediaService,
            SIMF.Infrastructure.Media.PublicMediaService>();
        services.AddScoped<SIMF.Application.Media.Abstractions.IAdminMediaService,
            SIMF.Infrastructure.Media.AdminMediaService>();
        // The media-gallery + shared image-asset bytes now live
        // in the unified StoredFile store; the bespoke MediaImage / ImageAsset stores
        // are gone (AssetService is retained, rewritten onto IFileService).
        services.AddScoped<SIMF.Application.Assets.Abstractions.IAssetService,
            SIMF.Infrastructure.Assets.AssetService>();
        // The externally hosted feeds — live streams, sign language, the summary
        // and gallery videos, the hero background — held in that same store.
        services.AddScoped<SIMF.Application.Files.Abstractions.IFeedLinkService,
            SIMF.Infrastructure.Files.FeedLinkService>();
        services.AddScoped<SIMF.Infrastructure.Configuration.HeroVideoUrlResolver>();
        services.AddScoped<SIMF.Application.Exhibition.Abstractions.IPublicBoothService,
            SIMF.Infrastructure.Exhibition.PublicBoothService>();
        services.AddScoped<SIMF.Application.Exhibition.Abstractions.IAdminBoothService,
            SIMF.Infrastructure.Exhibition.AdminBoothService>();
        // Organisation lookup (gov Excel import) + visitor picker search.
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IAdminOrganisationService,
            SIMF.Infrastructure.Organisations.AdminOrganisationService>();
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IPublicOrganisationService,
            SIMF.Infrastructure.Organisations.PublicOrganisationService>();
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IOrganisationExcelReader,
            SIMF.Infrastructure.Excel.ClosedXmlOrganisationReader>();
        // Region lookup — admin CRUD + public app picker read (the 13 official
        // Saudi regions). Seeded in every environment (required reference data).
        services.AddScoped<SIMF.Application.Regions.Abstractions.IAdminRegionService,
            SIMF.Infrastructure.Regions.AdminRegionService>();
        services.AddScoped<SIMF.Application.Regions.Abstractions.IPublicRegionService,
            SIMF.Infrastructure.Regions.PublicRegionService>();
        services.AddScoped<SIMF.Infrastructure.Regions.RegionSeeder>();
        // Default app-update config keys so the CP configuration grid
        // is not empty on a fresh DB. Idempotent. (The 2026 event CONTENT it
        // used to seed moved to the by-hand SQL lane.)
        services.AddScoped<SIMF.Infrastructure.Seeding.DefaultContentSeeder>();
        // Development/Testing runner for the by-hand 2026 content SQL
        // (docs/migrations/2026/*.sql) so a fresh dev/test DB is not empty.
        // Production never invokes it — content is applied by hand there.
        services.AddScoped<SIMF.Infrastructure.Seeding.SqlContentSeeder>();
        // The demo OPERATIONAL configuration (gates + operator
        // assignment, per-session moderator grants, the main hall's seat grid)
        // the demo accounts need before the scanner / moderation-desk / seat-picker
        // journeys can be exercised. Self-gated to Development or an explicit
        // Seed:EnableDemoAccounts, exactly like the demo accounts themselves.
        services.AddScoped<SIMF.Infrastructure.Seeding.DemoOperationalConfigSeeder>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IPublicSponsorService,
            SIMF.Infrastructure.Sponsors.PublicSponsorService>();
        // Anonymous public delegations (الوفود) view: the invited countries.
        services.AddScoped<SIMF.Application.Delegations.Abstractions.IPublicDelegationService,
            SIMF.Infrastructure.Delegations.PublicDelegationService>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IAdminSponsorService,
            SIMF.Infrastructure.Sponsors.AdminSponsorService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IPublicArchiveService,
            SIMF.Infrastructure.Archive.PublicArchiveService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IAdminArchiveService,
            SIMF.Infrastructure.Archive.AdminArchiveService>();
        // Advisory question AI filter.
        // Default = the offline stub (the PoC needs no AI key); set
        // `SessionQuestions:AiFilterEnabled=true` to route through the real
        // IAiService + the seeded `question-filter` prompt (AiQuestionFilter,
        // scoped because IAiService is scoped). Advisory either way — it never
        // blocks a question. Mirrors the scan-engine selector below.
        if (configuration.GetValue<bool>("SessionQuestions:AiFilterEnabled"))
        {
            services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.IQuestionAiFilter,
                SIMF.Infrastructure.SessionQuestions.AiQuestionFilter>();
        }
        else
        {
            services.AddSingleton<SIMF.Application.SessionQuestions.Abstractions.IQuestionAiFilter,
                SIMF.Infrastructure.SessionQuestions.StubQuestionAiFilter>();
        }
        // Dynamic, config-driven ratings — app form/submit, admin config CRUD,
        // admin responses + KPI viewer, and the built-in-types seeder.
        services.AddScoped<SIMF.Application.Feedback.Abstractions.IRatingFormService,
            SIMF.Infrastructure.Feedback.RatingFormService>();
        services.AddScoped<SIMF.Application.Feedback.Abstractions.IAdminRatingConfigService,
            SIMF.Infrastructure.Feedback.AdminRatingConfigService>();
        services.AddScoped<SIMF.Application.Feedback.Abstractions.IAdminRatingResponseService,
            SIMF.Infrastructure.Feedback.AdminRatingResponseService>();
        services.AddScoped<SIMF.Infrastructure.Feedback.RatingSeeder>();
        // Visitor-to-visitor networking connections (app-facing).
        services.AddScoped<SIMF.Application.Networking.Abstractions.INetworkingService,
            SIMF.Infrastructure.Networking.NetworkingService>();
        // "Meet People Like You" partner directory (app-facing).
        services.AddScoped<SIMF.Application.Networking.Abstractions.IPartnerDirectoryService,
            SIMF.Infrastructure.Networking.PartnerDirectoryService>();
        // Visitor-to-visitor contact sharing.
        services.AddScoped<SIMF.Application.Contacts.Abstractions.IVisitorShareService,
            SIMF.Infrastructure.Contacts.VisitorShareService>();
        // Exhibitor ("Other") lead capture: scan visitor badge → My Visitors.
        services.AddScoped<SIMF.Application.Exhibitors.Abstractions.IExhibitorVisitorService,
            SIMF.Infrastructure.Exhibitors.ExhibitorVisitorService>();
        // Dynamic session-category lookup.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionCategoryService,
            SIMF.Infrastructure.Programme.AdminSessionCategoryService>();
        // Programme days (date + title + logo).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminProgrammeDayService,
            SIMF.Infrastructure.Programme.AdminProgrammeDayService>();
        // Forum-day window (MIN/MAX over active ProgrammeDay.Date); bounds
        // business-meeting + speaker-availability scheduling to the event days.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IForumWindowService,
            SIMF.Infrastructure.Programme.ForumWindowService>();
        // Statistics dashboard (read-only aggregate) +
        // Exhibitor provisioning.
        services.AddScoped<SIMF.Application.Statistics.Abstractions.IStatisticsService,
            SIMF.Infrastructure.Statistics.StatisticsService>();
        // Reporting module — date-ranged read-only reports with XLSX export.
        services.AddScoped<SIMF.Application.Reporting.Abstractions.IReportingService,
            SIMF.Infrastructure.Reporting.ReportingService>();
        // Read-only session-attendance dashboard over HallAttendance.
        services.AddScoped<SIMF.Application.Attendance.Abstractions.ISessionAttendanceService,
            SIMF.Infrastructure.Attendance.SessionAttendanceService>();
        services.AddScoped<SIMF.Application.Exhibitors.Abstractions.IAdminExhibitorService,
            SIMF.Infrastructure.Exhibitors.AdminExhibitorService>();
        // Centralised AI module: prompt
        // catalogue + invocation log + Echo (offline) + OpenAI HTTP.
        // HttpClient registered as a singleton (no AddHttpClient since
        // the Infrastructure csproj does not reference
        // Microsoft.Extensions.Http; adding a package needs owner
        // approval per CLAUDE.md §1.7. The Echo provider is the only
        // path tests exercise, so the singleton-per-process HTTP
        // client is fine for the current scope).
        services.Configure<SIMF.Infrastructure.Ai.AiOptions>(
            configuration.GetSection(SIMF.Infrastructure.Ai.AiOptions.SectionName));
        // Install the HMAC key for prompt-content drift hashes.
        // Reads from `Ai:PromptHash:Secret` (env var
        // `SIMF_API_Ai__PromptHash__Secret` in production). If empty, the
        // helper uses a deterministic dev-mode derivation so tests pass
        // without configuration — production should always supply.
        // Hosting layers should check
        // `AiAuditDetail.IsHmacKeyDevFallback` at startup and refuse to
        // start (or page on-call) in production when the secret is
        // unconfigured. The flag is set as a side-effect of this call.
        SIMF.Infrastructure.Ai.AiAuditDetail.ConfigureHmacKey(
            configuration.GetValue<string?>(
                $"{SIMF.Infrastructure.Ai.AiOptions.SectionName}:PromptHash:Secret"));
        // Install the keyed-HMAC keys for AccountCode (OTP) hashing and for the
        // speaker action-link tokens. Both take the JWT signing key — a required,
        // boot-validated secret — as their master, and each derives its own subkey
        // from it, so passing one value here does not give them one key.
        SIMF.Application.IdentityAccess.AccountCodeHasher.ConfigureKey(
            configuration[$"{SIMF.Common.Options.JwtOptions.SectionName}:SigningKey"]);
        SIMF.Application.MeetingRequests.MeetingActionTokenHasher.ConfigureKey(
            configuration[$"{SIMF.Common.Options.JwtOptions.SectionName}:SigningKey"]);
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider,
            SIMF.Infrastructure.Ai.EchoAiProvider>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<SIMF.Infrastructure.Ai.OpenAiProvider>();
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider>(sp =>
            sp.GetRequiredService<SIMF.Infrastructure.Ai.OpenAiProvider>());
        // Anthropic (Claude) Messages-API provider (shares the singleton HttpClient).
        services.AddSingleton<SIMF.Infrastructure.Ai.AnthropicAiProvider>();
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider>(sp =>
            sp.GetRequiredService<SIMF.Infrastructure.Ai.AnthropicAiProvider>());
        // Google Gemini (Generative Language API) provider (shares the singleton HttpClient).
        services.AddSingleton<SIMF.Infrastructure.Ai.GeminiAiProvider>();
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider>(sp =>
            sp.GetRequiredService<SIMF.Infrastructure.Ai.GeminiAiProvider>());
        services.AddSingleton<IReadOnlyDictionary<SIMF.Common.Enums.AiProvider,
                SIMF.Application.Ai.Abstractions.IAiProvider>>(sp =>
            sp.GetServices<SIMF.Application.Ai.Abstractions.IAiProvider>()
                .ToDictionary(p => p.Tag));
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAiService,
            SIMF.Infrastructure.Ai.AiService>();
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAdminAiPromptService,
            SIMF.Infrastructure.Ai.AdminAiPromptService>();
        // Grounds the app AI assistant (assistance prompt) on the live event data
        // — reuses the same public read services the app's own screens call.
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAssistanceContextBuilder,
            SIMF.Infrastructure.Ai.AssistanceContextBuilder>();
        // Persists the app AI assistant's per-user conversation so it
        // survives navigation/restart and the assistant remembers earlier turns.
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAiChatHistoryService,
            SIMF.Infrastructure.Ai.AiChatHistoryService>();
        // Transactional-email templates: the resolver (DB override else
        // code default) and the CP admin service.
        services.AddScoped<SIMF.Application.Email.IEmailTemplateResolver,
            SIMF.Infrastructure.Email.EmailTemplateResolver>();
        services.AddScoped<SIMF.Application.Email.IAdminEmailTemplateService,
            SIMF.Infrastructure.Email.AdminEmailTemplateService>();
        // Server-side subtitle fetch from a video (YouTube) for the CP
        // Sessions editor. Uses a DEDICATED no-redirect HttpClient (not the shared
        // singleton): the caption baseUrl comes from YouTube's response, so following
        // a 3xx into an internal host would be SSRF; with
        // AllowAutoRedirect=false a redirect fails closed (the service also re-validates
        // the baseUrl host). BCL-only — no Microsoft.Extensions.Http package (§1.7).
        var youtubeTranscriptHttp = new HttpClient(
            new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        services.AddScoped<SIMF.Application.Programme.Abstractions.IYoutubeTranscriptService>(
            sp => new SIMF.Infrastructure.Programme.YoutubeTranscriptService(
                youtubeTranscriptHttp,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<
                    SIMF.Infrastructure.Programme.YoutubeTranscriptService>>()));
        // Gate Module: admin CRUD + operator surface + QR resolver +
        // gate-config cache + idempotency store + failure-rate circuit.
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IQrResolver,
            SIMF.Infrastructure.AccessControl.QrResolver>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IAdminGateService,
            SIMF.Infrastructure.AccessControl.AdminGateService>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IGateOperatorService,
            SIMF.Infrastructure.AccessControl.GateOperatorService>();
        services.AddScoped<SIMF.Application.AccessControl.Abstractions.IGateConfigCache,
            SIMF.Infrastructure.AccessControl.GateConfigCache>();
        // The failure-rate circuit is singleton (in-memory state per process).
        services.AddSingleton<SIMF.Application.AccessControl.Abstractions.IGateFailureCircuit,
            SIMF.Infrastructure.AccessControl.GateFailureCircuit>();
        services.AddScoped<IAdminApprovalReadService, AdminApprovalReadService>();
        // VVIP/VIP welcome roster read + CSV/Excel export (موج).
        services.AddScoped<IVipRosterService, VipRosterService>();
        services.AddScoped<IQrIdMinter, QrIdMinter>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        // The offline human-face gate on the profile-image
        // upload. Singleton: the ONNX detector session is expensive.
        services.Configure<FaceDetectionOptions>(
            configuration.GetSection(FaceDetectionOptions.SectionName));
        services.AddSingleton<IFaceDetectionService, FaceAiSharpFaceDetectionService>();
        services.AddScoped<IInterestService, InterestService>();
        // The dispatcher delivers through the registered
        // INotificationChannel set (ascending Order: in-app 0, email 10) instead of
        // two hard-coded deliveries. An SMS / WhatsApp channel is one more line here
        // once a gateway is procured (owner-action); no dispatcher change.
        services.AddScoped<INotificationChannel, InAppNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationBroadcastService,
            SIMF.Infrastructure.Notifications.NotificationBroadcastService>();
        services.AddSingleton<IUserExcelService, ClosedXmlUserExcelService>();
        // Export-only workbook builders for the read-only admin grids.
        services.AddSingleton<IOperationLogExcelService, ClosedXmlOperationLogExcelService>();
        services.AddSingleton<IAttendeeExcelService, ClosedXmlAttendeeExcelService>();
        // Generic grid Excel engine (one hardened exporter/importer for
        // every resource's export/import, driven by per-resource column descriptors).
        services.AddSingleton<SIMF.Application.Excel.IGridExcelExporter, ClosedXmlGridExcelExporter>();
        services.AddSingleton<SIMF.Application.Excel.IGridExcelImporter, ClosedXmlGridExcelImporter>();
        // The avatar, VVIP welcome-photo and ID-document
        // bytes now live in the unified StoredFile store; the bespoke user-keyed
        // stores (FilesystemAvatarStorage / FilesystemVipPhotoStorage /
        // EncryptedUserIdDocumentStorage) are gone.
        // A2-10 — AES-GCM encryptor for PII identifier columns; applied by an EF
        // value converter on UserProfile (SimfAppDbContext.OnModelCreating).
        services.AddSingleton<SIMF.Application.Abstractions.IPiiEncryptor, AesGcmPiiEncryptor>();
        // A6-18 — upload malware scanner (EICAR default; swap for ClamAV/Defender).
        // The malware-scan engine. "ClamAV" wires the real
        // clamd daemon (production); anything else keeps the built-in EICAR
        // detector. The centralized file pipeline runs whichever is registered
        // fail-closed in Production.
        var scanEngine = configuration.GetSection(UploadScanningOptions.SectionName)["Engine"];
        if (string.Equals(scanEngine, "ClamAV", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SIMF.Application.Abstractions.IUploadScanner,
                SIMF.Infrastructure.Files.ClamAvUploadScanner>();
        }
        else
        {
            services.AddSingleton<SIMF.Application.Abstractions.IUploadScanner, DefaultUploadScanner>();
        }

        // The centralized file store: one envelope cipher + one storage
        // provider behind the single StoredFile pipeline. The cipher boot-fails on
        // a missing/invalid KEK the first time it is resolved (same posture as the
        // ID-document key). Both are stateless singletons.
        services.Configure<FileStorageOptions>(
            configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<SIMF.Application.Files.Abstractions.IFileCipher,
            SIMF.Infrastructure.Files.AesGcmEnvelopeCipher>();
        services.AddSingleton<SIMF.Application.Files.Abstractions.IFileStorageProvider,
            SIMF.Infrastructure.Files.FilesystemFileStorageProvider>();
        services.AddScoped<SIMF.Application.Files.Abstractions.IFileService,
            SIMF.Infrastructure.Files.StoredFileService>();
        services.AddSingleton<ILogFileService, LogFileService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITotpVerifier, TotpVerifier>();
        services.AddScoped<IdentitySeeder>();

        // Email — a singleton queue and sender drained by a background worker,
        // so a slow mail server never blocks a request.
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailBackgroundService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
