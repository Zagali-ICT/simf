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
        // Stamps CreatedBy/CreatedAt + UpdatedBy/UpdatedAt on every audited App
        // entity (BaseAuditEntity) from the signed-in actor. Scoped so it sees
        // the per-request IRequestContext. Registered BEFORE the row-audit
        // interceptor below so the stamped values land in the audit trail.
        services.AddScoped<AuditStampingSaveChangesInterceptor>();

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
            }).AddInterceptors(
                sp.GetRequiredService<AuditStampingSaveChangesInterceptor>(),
                sp.GetRequiredService<RowAuditingSaveChangesInterceptor>()));

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
        // R4 — D-209: InterestService moved to Application; its EF query
        // shapes live behind IInterestRepository (over SimfAppDbContext).
        services.AddScoped<IInterestRepository, InterestRepository>();
        // R4 — D-209: UserProfileService moved to Application; it spans both
        // DBs (profile + lookups on App, account reads + transactional save on
        // Identity) behind IUserProfileRepository.
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        // Issue-1 — resolves a user's permission codes from their roles for
        // the `perm` claim baked into the JWT (Administrator → wildcard).
        services.AddScoped<IPermissionResolver, PermissionResolver>();
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
        // P2.3 — D-228 (FR-407): speaker presentation-file management + storage.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSpeakerPresentationService,
            SIMF.Infrastructure.Programme.AdminSpeakerPresentationService>();
        // P2.4 — D-229 (FDS-012 §5.5): System Configuration settings store.
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IAdminSystemSettingService,
            SIMF.Infrastructure.Configuration.AdminSystemSettingService>();
        // P2.5 — D-230 (FR-605): 2D venue map (admin CRUD + public read).
        services.AddScoped<SIMF.Application.Venue.Abstractions.IVenueMapService,
            SIMF.Infrastructure.Venue.VenueMapService>();
        services.Configure<SIMF.Infrastructure.Programme.SpeakerPresentationStorageOptions>(
            configuration.GetSection(SIMF.Infrastructure.Programme.SpeakerPresentationStorageOptions.SectionName));
        services.AddSingleton<SIMF.Application.Programme.Abstractions.ISpeakerPresentationStorage,
            SIMF.Infrastructure.Programme.FilesystemSpeakerPresentationStorage>();
        // D-165 (gap doc G3) — Session admin CRUD: programme sessions tied
        // to a Hall + M-to-M Speakers + M-to-M Themes (PDF §2.9).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionService,
            SIMF.Infrastructure.Programme.AdminSessionService>();
        // P4.1 — D-238: AI session-summary / محضر committee desk (drafts via
        // the central IAiService seam; publishes for the app read in D-237).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionSummaryService,
            SIMF.Infrastructure.Programme.AdminSessionSummaryService>();
        // P5.1 — D-241: attendee-facing hall arrival/departure via GPS geofence.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IHallAttendanceService,
            SIMF.Infrastructure.Programme.HallAttendanceService>();
        // P3.2b — D-232 (D-213): out-of-row session-recording storage (streamed
        // both ways, range-served behind a short-lived stream token).
        services.Configure<SIMF.Infrastructure.Programme.SessionRecordingStorageOptions>(
            configuration.GetSection(SIMF.Infrastructure.Programme.SessionRecordingStorageOptions.SectionName));
        services.AddSingleton<SIMF.Application.Programme.Abstractions.ISessionRecordingStorage,
            SIMF.Infrastructure.Programme.FilesystemSessionRecordingStorage>();
        // D-166 (gap doc G4) — registration gate + archive visibility
        // singletons + the auto-close background worker (PDF §2.3, §2.4).
        services.AddScoped<SIMF.Application.Operations.Abstractions.IOperationsToggleService,
            SIMF.Infrastructure.Operations.OperationsToggleService>();
        services.AddHostedService<SIMF.Infrastructure.Operations.RegistrationGateAutoCloseWorker>();
        // P1.7 (D-217) — automated "session starting soon" reminder worker.
        services.AddHostedService<SIMF.Infrastructure.Operations.SessionReminderWorker>();
        // D-168 (gap doc G5) — public-relations team: invitation CRUD +
        // VIP list + bulk-notify dispatcher (PDF §2.7.3).
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminInvitationService,
            SIMF.Infrastructure.PublicRelations.AdminInvitationService>();
        // D-169 (gap doc G6) — session-question moderation: public submit
        // + per-session moderator queue + admin assignment of moderators
        // (PDF §2.7.2, distinct from MobileAppRole.Moderator).
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionQuestionService,
            SIMF.Infrastructure.SessionQuestions.SessionQuestionService>();
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionModerationService,
            SIMF.Infrastructure.SessionQuestions.SessionModerationService>();
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.IAdminSessionModeratorService,
            SIMF.Infrastructure.SessionQuestions.AdminSessionModeratorService>();
        // P3.3 — D-212/D-234: Scientific-Committee central Q&A queue (stage 2).
        services.AddScoped<SIMF.Application.SessionQuestions.Abstractions.ISessionQuestionCommitteeService,
            SIMF.Infrastructure.SessionQuestions.SessionQuestionCommitteeService>();
        // D-170 (gap doc G9) — "Meet People Like You" interest-intersection
        // ranker (PDF §2.8). Read-only service over UserProfile.Interests.
        services.AddScoped<SIMF.Application.Recommendations.Abstractions.IRecommendationService,
            SIMF.Infrastructure.Recommendations.RecommendationService>();
        // D-172 (gap doc G10) — Face ID / Touch ID biometric sign-in via
        // ECDSA P-256 device key (PDF §2.5).
        services.AddScoped<SIMF.Application.IdentityAccess.Abstractions.IDeviceKeyService,
            SIMF.Infrastructure.IdentityAccess.DeviceKeyService>();
        // D-173 (gap doc G8) — Dynamic content CMS: admin CRUD over
        // ContentBlock + Banner (PDF §1, §2.1), plus the public read
        // surface for the Flutter app + Website.
        services.AddScoped<SIMF.Application.Cms.Abstractions.IAdminCmsService,
            SIMF.Infrastructure.Cms.AdminCmsService>();
        services.AddScoped<SIMF.Application.Cms.Abstractions.IPublicCmsService,
            SIMF.Infrastructure.Cms.PublicCmsService>();
        // D-269 (Mockup page 20) — attendee meeting requests to a speaker.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.ISpeakerMeetingRequestService,
            SIMF.Infrastructure.MeetingRequests.SpeakerMeetingRequestService>();
        // D-175 (gap doc G11, Mockup page 7) — per-session seat
        // reservations (visitor self-pick + random + admin row blocks).
        services.AddScoped<SIMF.Application.SeatReservations.Abstractions.ISeatReservationService,
            SIMF.Infrastructure.SeatReservations.SeatReservationService>();
        // SIMF-FDS-013 — D-248: flexible hall config + admin-arranged B2B/B2C
        // business meetings (meeting tables, hall allocations, meetings).
        services.AddScoped<SIMF.Application.BusinessMeetings.Abstractions.IBusinessMeetingService,
            SIMF.Infrastructure.BusinessMeetings.BusinessMeetingService>();
        // D-249 — App Screen 14 My-Area dashboard (held bookings + accepted
        // speaker meetings + confirmed business meetings + identity card).
        services.AddScoped<SIMF.Application.MyArea.IMyAreaService,
            SIMF.Infrastructure.MyArea.MyAreaService>();
        // D-199 — event modules (freeze lift): programme/speaker public reads,
        // news, media + media-partners, booths, sponsors, archive, comments, ratings.
        services.AddScoped<SIMF.Application.Programme.Abstractions.IPublicSpeakerService,
            SIMF.Infrastructure.Programme.PublicSpeakerService>();
        services.AddScoped<SIMF.Application.Programme.Abstractions.IProgrammeSessionService,
            SIMF.Infrastructure.Programme.ProgrammeSessionService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IPublicNewsService,
            SIMF.Infrastructure.PublicRelations.PublicNewsService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminNewsService,
            SIMF.Infrastructure.PublicRelations.AdminNewsService>();
        // P2.1 (D-211) — FAQ management (two-level group → entry).
        services.AddScoped<SIMF.Application.Faq.Abstractions.IAdminFaqService,
            SIMF.Infrastructure.Faq.AdminFaqService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IPublicMediaPartnerService,
            SIMF.Infrastructure.PublicRelations.PublicMediaPartnerService>();
        services.AddScoped<SIMF.Application.PublicRelations.Abstractions.IAdminMediaPartnerService,
            SIMF.Infrastructure.PublicRelations.AdminMediaPartnerService>();
        services.AddScoped<SIMF.Application.Media.Abstractions.IPublicMediaService,
            SIMF.Infrastructure.Media.PublicMediaService>();
        services.AddScoped<SIMF.Application.Media.Abstractions.IAdminMediaService,
            SIMF.Infrastructure.Media.AdminMediaService>();
        services.Configure<SIMF.Infrastructure.Media.MediaImageStorageOptions>(
            configuration.GetSection(SIMF.Infrastructure.Media.MediaImageStorageOptions.SectionName));
        services.AddSingleton<SIMF.Application.Abstractions.IMediaImageStorage,
            SIMF.Infrastructure.Media.FilesystemMediaImageStorage>();
        services.AddScoped<SIMF.Application.Exhibition.Abstractions.IPublicBoothService,
            SIMF.Infrastructure.Exhibition.PublicBoothService>();
        services.AddScoped<SIMF.Application.Exhibition.Abstractions.IAdminBoothService,
            SIMF.Infrastructure.Exhibition.AdminBoothService>();
        // B3 (D-220) — Organisation lookup (gov Excel import) + visitor picker search.
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IAdminOrganisationService,
            SIMF.Infrastructure.Organisations.AdminOrganisationService>();
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IPublicOrganisationService,
            SIMF.Infrastructure.Organisations.PublicOrganisationService>();
        services.AddScoped<SIMF.Application.Organisations.Abstractions.IOrganisationExcelReader,
            SIMF.Infrastructure.Excel.ClosedXmlOrganisationReader>();
        // B3 — D-221 — dev-only sample-organisation seeder (Program.cs runs it in
        // Development only; production uses the gov Excel import).
        services.AddScoped<SIMF.Infrastructure.Organisations.OrganisationSeeder>();
        // SIMF-FDS-014 (D-261) — shared Contact directory admin CRUD.
        services.AddScoped<SIMF.Application.Contacts.Abstractions.IAdminContactService,
            SIMF.Infrastructure.Contacts.AdminContactService>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IPublicSponsorService,
            SIMF.Infrastructure.Sponsors.PublicSponsorService>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IAdminSponsorService,
            SIMF.Infrastructure.Sponsors.AdminSponsorService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IPublicArchiveService,
            SIMF.Infrastructure.Archive.PublicArchiveService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IAdminArchiveService,
            SIMF.Infrastructure.Archive.AdminArchiveService>();
        services.AddScoped<SIMF.Application.SessionComments.Abstractions.ISessionCommentService,
            SIMF.Infrastructure.SessionComments.SessionCommentService>();
        services.AddScoped<SIMF.Application.SessionComments.Abstractions.IAdminSessionCommentService,
            SIMF.Infrastructure.SessionComments.AdminSessionCommentService>();
        // Stateless (reads only IOptions) — register as singleton.
        services.AddSingleton<SIMF.Application.SessionComments.Abstractions.ICommentAiFilter,
            SIMF.Infrastructure.SessionComments.StubCommentAiFilter>();
        // P4.2 — D-236: advisory question AI filter (stub). Stateless singleton.
        services.AddSingleton<SIMF.Application.SessionQuestions.Abstractions.IQuestionAiFilter,
            SIMF.Infrastructure.SessionQuestions.StubQuestionAiFilter>();
        services.AddScoped<SIMF.Application.Feedback.Abstractions.IRatingService,
            SIMF.Infrastructure.Feedback.RatingService>();
        // B6 — D-224: visitor-to-visitor networking connections (app-facing).
        services.AddScoped<SIMF.Application.Networking.Abstractions.INetworkingService,
            SIMF.Infrastructure.Networking.NetworkingService>();
        // SIMF-FDS-014 — D-284 (Track 2): visitor-to-visitor contact sharing.
        services.AddScoped<SIMF.Application.Contacts.Abstractions.IVisitorShareService,
            SIMF.Infrastructure.Contacts.VisitorShareService>();
        // B9b — D-226: dynamic session-category lookup (FDS-004 §5.4).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionCategoryService,
            SIMF.Infrastructure.Programme.AdminSessionCategoryService>();
        // D-202 — Track-2: Statistics dashboard (read-only aggregate) +
        // Exhibitor provisioning.
        services.AddScoped<SIMF.Application.Statistics.Abstractions.IStatisticsService,
            SIMF.Infrastructure.Statistics.StatisticsService>();
        // FR-506 — read-only session-attendance dashboard over HallAttendance (D-241).
        services.AddScoped<SIMF.Application.Attendance.Abstractions.ISessionAttendanceService,
            SIMF.Infrastructure.Attendance.SessionAttendanceService>();
        services.AddScoped<SIMF.Application.Exhibitors.Abstractions.IAdminExhibitorService,
            SIMF.Infrastructure.Exhibitors.AdminExhibitorService>();
        // D-176 (gap doc G12) — centralised AI module: prompt
        // catalogue + invocation log + Echo (offline) + OpenAI HTTP.
        // HttpClient registered as a singleton (no AddHttpClient since
        // the Infrastructure csproj does not reference
        // Microsoft.Extensions.Http; adding a package needs owner
        // approval per CLAUDE.md §1.7. The Echo provider is the only
        // path tests exercise, so the singleton-per-process HTTP
        // client is fine for the current scope).
        services.Configure<SIMF.Infrastructure.Ai.AiOptions>(
            configuration.GetSection(SIMF.Infrastructure.Ai.AiOptions.SectionName));
        // D-181 — install the HMAC key for prompt-content drift hashes.
        // Reads from `Ai:PromptHash:Secret` (env var
        // `SIMF_Ai__PromptHash__Secret` in production). If empty, the
        // helper uses a deterministic dev-mode derivation so tests pass
        // without configuration — production should always supply.
        // D-181 (review-pass) — hosting layers should check
        // `AiAuditDetail.IsHmacKeyDevFallback` at startup and refuse to
        // start (or page on-call) in production when the secret is
        // unconfigured. The flag is set as a side-effect of this call.
        SIMF.Infrastructure.Ai.AiAuditDetail.ConfigureHmacKey(
            configuration.GetValue<string?>(
                $"{SIMF.Infrastructure.Ai.AiOptions.SectionName}:PromptHash:Secret"));
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider,
            SIMF.Infrastructure.Ai.EchoAiProvider>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<SIMF.Infrastructure.Ai.OpenAiProvider>();
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider>(sp =>
            sp.GetRequiredService<SIMF.Infrastructure.Ai.OpenAiProvider>());
        services.AddSingleton<IReadOnlyDictionary<SIMF.Common.Enums.AiProvider,
                SIMF.Application.Ai.Abstractions.IAiProvider>>(sp =>
            sp.GetServices<SIMF.Application.Ai.Abstractions.IAiProvider>()
                .ToDictionary(p => p.Tag));
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAiService,
            SIMF.Infrastructure.Ai.AiService>();
        services.AddScoped<SIMF.Application.Ai.Abstractions.IAdminAiPromptService,
            SIMF.Infrastructure.Ai.AdminAiPromptService>();
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
        // P1.6 — export-only workbook builders for the read-only admin grids.
        services.AddSingleton<IOperationLogExcelService, ClosedXmlOperationLogExcelService>();
        services.AddSingleton<IAttendeeExcelService, ClosedXmlAttendeeExcelService>();
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
