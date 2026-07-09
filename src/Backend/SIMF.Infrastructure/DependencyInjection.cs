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
    /// <summary>M4 (security) — refuse to start in Production when the AI
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
                + "SIMF_Ai__PromptHash__Secret before starting.");
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
                + "Set SIMF_Storage__UserIdDocumentEncryptionKey before starting.");
        }
    }

    /// <summary>D-568 — refuse to start in Production when the centralized
    /// file-store KEK (<c>FileStorage:EncryptionKey</c>) is missing. The
    /// <c>AesGcmEnvelopeCipher</c> already fail-fasts on construction, but that
    /// surfaces deep inside the DI / FastEndpoints stack; this guard states the
    /// one-line reason up front. Call from the host after the app is built.</summary>
    public static void EnsureFileStorageEncryptionConfigured(bool isProduction, IServiceProvider services)
    {
        if (isProduction
            && string.IsNullOrWhiteSpace(
                services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SIMF.Common.Options.FileStorageOptions>>()
                    .Value.EncryptionKey))
        {
            throw new InvalidOperationException(
                "FileStorage:EncryptionKey must be a base64 32-byte AES key in Production — "
                + "it is the KEK for the centralized file store (D-568). "
                + "Set SIMF_FileStorage__EncryptionKey before starting.");
        }
    }

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
        // The built-in validator enforces the SIMF-API-001 §12.5 length baseline;
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

                // Account lockout — the brute-force defence (SIMF-FDS-001 A.1).
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
        // D-585 — the demo user-account seed shares one password sourced here.
        services.Configure<DemoSeedOptions>(
            configuration.GetSection(DemoSeedOptions.SectionName));
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));
        // A7-13 (NCA) — credential-lifecycle settings (password max age).
        services.Configure<IdentityLifecycleOptions>(
            configuration.GetSection(IdentityLifecycleOptions.SectionName));
        // A6-18 (NCA) — upload malware-scanning settings.
        services.Configure<UploadScanningOptions>(
            configuration.GetSection(UploadScanningOptions.SectionName));
        // #7a — biometric device-key enrolment step-up toggle (default on).
        services.Configure<DeviceKeyOptions>(
            configuration.GetSection(DeviceKeyOptions.SectionName));
        // D-717 (item 7, FDS-013 §15 GAP-3) — the speaker email-link base URL + TTL.
        services.Configure<MeetingLinksOptions>(
            configuration.GetSection(MeetingLinksOptions.SectionName));
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
        // A7-20 (NCA) — retired-password-hash store for reuse prevention.
        services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
        // A1-19 (NCA) — dormant-account auto-disable (driven by the daily sweep host).
        services.AddScoped<IDormantAccountService, DormantAccountService>();
        // A4 (NCA data-minimisation) — retention purge of dead security artifacts
        // (driven by the daily RetentionSweepWorker host).
        services.AddScoped<IRetentionPurgeService, RetentionPurgeService>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        // Phase-6 de-dup (D-636) — resolves Identity-owned user attributes for
        // App-side services across the DB boundary (D-157: no cross-DB JOIN).
        services.AddScoped<IIdentityUserDirectory, IdentityUserDirectory>();
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
        // Part B — badge-QR sign-in / activation.
        services.AddScoped<IBadgeAuthService, BadgeAuthService>();
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
        // D-461 — public read-path over the whitelisted site-settings keys
        // (registration welcome message + social links).
        services.AddScoped<SIMF.Application.Configuration.Abstractions.ISiteSettingsService,
            SIMF.Infrastructure.Configuration.SiteSettingsService>();
        // D-495 — the singleton Organization / About profile: cached public read +
        // admin full-document upsert (the edition-generic forum config).
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IOrganizationProfileReadService,
            SIMF.Infrastructure.Configuration.OrganizationProfileReadService>();
        services.AddScoped<SIMF.Application.Configuration.Abstractions.IOrganizationProfileAdminService,
            SIMF.Infrastructure.Configuration.OrganizationProfileAdminService>();
        // P2.5 — D-230 (FR-605): 2D venue map (admin CRUD + public read).
        services.AddScoped<SIMF.Application.Venue.Abstractions.IVenueMapService,
            SIMF.Infrastructure.Venue.VenueMapService>();
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
        // D-568 (Wave C S7): recordings now live in the unified StoredFile store;
        // SessionRecordingStorageOptions is kept only for the upload endpoint's
        // MaxUploadBytes ceiling (the bespoke recording store is gone).
        services.Configure<SIMF.Infrastructure.Programme.SessionRecordingStorageOptions>(
            configuration.GetSection(SIMF.Infrastructure.Programme.SessionRecordingStorageOptions.SectionName));
        // D-166 (gap doc G4) — registration gate + archive visibility
        // singletons + the auto-close background worker (PDF §2.3, §2.4).
        services.AddScoped<SIMF.Application.Operations.Abstractions.IOperationsToggleService,
            SIMF.Infrastructure.Operations.OperationsToggleService>();
        services.AddHostedService<SIMF.Infrastructure.Operations.RegistrationGateAutoCloseWorker>();
        // P1.7 (D-217) — automated "session starting soon" reminder worker.
        services.AddHostedService<SIMF.Infrastructure.Operations.SessionReminderWorker>();
        // End-of-session "please rate this session" prompt worker.
        services.AddHostedService<SIMF.Infrastructure.Operations.SessionRatingPromptWorker>();
        // D-679 — end-of-day + end-of-programme rating prompt worker.
        services.AddHostedService<SIMF.Infrastructure.Operations.ProgrammeRatingPromptWorker>();
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
        // D-474 (#11, Group G) — speaker availability windows + free-slot derivation.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.ISpeakerAvailabilityService,
            SIMF.Infrastructure.MeetingRequests.SpeakerAvailabilityService>();
        // D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows (hall time
        // for business meetings) + free-slot derivation.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IHallAvailabilityService,
            SIMF.Infrastructure.MeetingRequests.HallAvailabilityService>();
        // D-717 (item 7, FDS-013 §15.7 GAP-3) — speaker double-opt-in action tokens.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IMeetingActionTokenService,
            SIMF.Infrastructure.MeetingRequests.MeetingActionTokenService>();
        // D-478 (#11, Group G phase 2) — delegation↔delegation meeting requests.
        services.AddScoped<SIMF.Application.MeetingRequests.Abstractions.IDelegationMeetingRequestService,
            SIMF.Infrastructure.MeetingRequests.DelegationMeetingRequestService>();
        // D-500 (Wave 5, الطلبات) — the unified "My requests" feed (supersedes the
        // old read-only My-meetings feed, D-479) + the two new standalone request
        // types (participation-document + badge-update).
        services.AddScoped<SIMF.Application.Requests.Abstractions.IMyRequestsService,
            SIMF.Infrastructure.Requests.MyRequestsService>();
        services.AddScoped<SIMF.Application.Requests.Abstractions.IParticipationDocumentRequestService,
            SIMF.Infrastructure.Requests.ParticipationDocumentRequestService>();
        services.AddScoped<SIMF.Application.Requests.Abstractions.IBadgeUpdateRequestService,
            SIMF.Infrastructure.Requests.BadgeUpdateRequestService>();
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
        // news, media + media-partners, booths, sponsors, archive, ratings.
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
        // Public, anonymous FAQ read for the app accordion (Figma 1388:7567).
        services.AddScoped<SIMF.Application.Faq.Abstractions.IPublicFaqService,
            SIMF.Infrastructure.Faq.PublicFaqService>();
        // Contact-us inquiries — public submit + CP inbox (Figma 1388:7567).
        services.AddScoped<SIMF.Application.Support.Abstractions.IContactInquiryService,
            SIMF.Infrastructure.Support.ContactInquiryService>();
        // Session favourites (المفضلة) — heart toggle on summaries + my-sessions.
        services.AddScoped<SIMF.Application.Programme.Abstractions.ISessionFavouriteService,
            SIMF.Infrastructure.Programme.SessionFavouriteService>();
        // Wave 2 — public read + download of speaker presentations (Figma 1388:7621).
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
        // D-568 (Wave C S1/S2): the media-gallery + shared image-asset bytes now live
        // in the unified StoredFile store; the bespoke MediaImage / ImageAsset stores
        // are gone (AssetService is retained, rewritten onto IFileService).
        services.AddScoped<SIMF.Application.Assets.Abstractions.IAssetService,
            SIMF.Infrastructure.Assets.AssetService>();
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
        // Region lookup — admin CRUD + public app picker read (the 13 official
        // Saudi regions). Seeded in every environment (required reference data).
        services.AddScoped<SIMF.Application.Regions.Abstractions.IAdminRegionService,
            SIMF.Infrastructure.Regions.AdminRegionService>();
        services.AddScoped<SIMF.Application.Regions.Abstractions.IPublicRegionService,
            SIMF.Infrastructure.Regions.PublicRegionService>();
        services.AddScoped<SIMF.Infrastructure.Regions.RegionSeeder>();
        // D-681 — default public content (hall, programme days + sessions,
        // highlights, org X link) so a fresh DB is not empty. Idempotent.
        services.AddScoped<SIMF.Infrastructure.Seeding.DefaultContentSeeder>();
        // SIMF-FDS-014 (D-261) — shared Contact directory admin CRUD.
        services.AddScoped<SIMF.Application.Contacts.Abstractions.IAdminContactService,
            SIMF.Infrastructure.Contacts.AdminContactService>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IPublicSponsorService,
            SIMF.Infrastructure.Sponsors.PublicSponsorService>();
        // D-499 (الوفود) — anonymous public delegations view (invited countries).
        services.AddScoped<SIMF.Application.Delegations.Abstractions.IPublicDelegationService,
            SIMF.Infrastructure.Delegations.PublicDelegationService>();
        services.AddScoped<SIMF.Application.Sponsors.Abstractions.IAdminSponsorService,
            SIMF.Infrastructure.Sponsors.AdminSponsorService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IPublicArchiveService,
            SIMF.Infrastructure.Archive.PublicArchiveService>();
        services.AddScoped<SIMF.Application.Archive.Abstractions.IAdminArchiveService,
            SIMF.Infrastructure.Archive.AdminArchiveService>();
        // P4.2 — D-236 / D-714 (item 12, GAP-1): advisory question AI filter.
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
        // B6 — D-224: visitor-to-visitor networking connections (app-facing).
        services.AddScoped<SIMF.Application.Networking.Abstractions.INetworkingService,
            SIMF.Infrastructure.Networking.NetworkingService>();
        // SIMF-FDS-014 — D-284 (Track 2): visitor-to-visitor contact sharing.
        services.AddScoped<SIMF.Application.Contacts.Abstractions.IVisitorShareService,
            SIMF.Infrastructure.Contacts.VisitorShareService>();
        // D-426 — exhibitor ("Other") lead capture: scan visitor badge → My Visitors.
        services.AddScoped<SIMF.Application.Exhibitors.Abstractions.IExhibitorVisitorService,
            SIMF.Infrastructure.Exhibitors.ExhibitorVisitorService>();
        // B9b — D-226: dynamic session-category lookup (FDS-004 §5.4).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminSessionCategoryService,
            SIMF.Infrastructure.Programme.AdminSessionCategoryService>();
        // D-452: programme days (date + title + logo).
        services.AddScoped<SIMF.Application.Programme.Abstractions.IAdminProgrammeDayService,
            SIMF.Infrastructure.Programme.AdminProgrammeDayService>();
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
        // M3 (security) — install the keyed-HMAC key for AccountCode (OTP)
        // hashing; reuses the JWT signing key (a required, boot-validated secret).
        SIMF.Application.IdentityAccess.AccountCodeHasher.ConfigureKey(
            configuration[$"{SIMF.Common.Options.JwtOptions.SectionName}:SigningKey"]);
        // D-717 (item 7, FDS-013 §15.7) — install the keyed-HMAC key for the speaker
        // action-link tokens; reuses the same boot-validated JWT signing key.
        SIMF.Application.MeetingRequests.MeetingActionTokenHasher.ConfigureKey(
            configuration[$"{SIMF.Common.Options.JwtOptions.SectionName}:SigningKey"]);
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider,
            SIMF.Infrastructure.Ai.EchoAiProvider>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<SIMF.Infrastructure.Ai.OpenAiProvider>();
        services.AddSingleton<SIMF.Application.Ai.Abstractions.IAiProvider>(sp =>
            sp.GetRequiredService<SIMF.Infrastructure.Ai.OpenAiProvider>());
        // D-484 — Anthropic (Claude) Messages-API provider (shares the singleton HttpClient).
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
        // D-578 — server-side subtitle fetch from a video (YouTube) for the CP
        // Sessions editor. Uses a DEDICATED no-redirect HttpClient (not the shared
        // singleton): the caption baseUrl comes from YouTube's response, so following
        // a 3xx into an internal host would be SSRF (D-578 security review #1); with
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
        // V-1 (D-429) — VVIP/VIP welcome roster read + CSV/Excel export (موج).
        services.AddScoped<IVipRosterService, VipRosterService>();
        services.AddScoped<IQrIdMinter, QrIdMinter>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        // C7 (D-371) — the offline human-face gate on the profile-image
        // upload. Singleton: the ONNX detector session is expensive.
        services.Configure<FaceDetectionOptions>(
            configuration.GetSection(FaceDetectionOptions.SectionName));
        services.AddSingleton<IFaceDetectionService, FaceAiSharpFaceDetectionService>();
        services.AddScoped<IInterestService, InterestService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IUserExcelService, ClosedXmlUserExcelService>();
        // P1.6 — export-only workbook builders for the read-only admin grids.
        services.AddSingleton<IOperationLogExcelService, ClosedXmlOperationLogExcelService>();
        services.AddSingleton<IAttendeeExcelService, ClosedXmlAttendeeExcelService>();
        // D-356 — generic grid Excel engine (one hardened exporter/importer for
        // every resource's export/import, driven by per-resource column descriptors).
        services.AddSingleton<SIMF.Application.Excel.IGridExcelExporter, ClosedXmlGridExcelExporter>();
        services.AddSingleton<SIMF.Application.Excel.IGridExcelImporter, ClosedXmlGridExcelImporter>();
        // D-568 (Wave C S3/S4/S5): the avatar, VVIP welcome-photo and ID-document
        // bytes now live in the unified StoredFile store; the bespoke user-keyed
        // stores (FilesystemAvatarStorage / FilesystemVipPhotoStorage /
        // EncryptedUserIdDocumentStorage) are gone.
        // A2-10 — AES-GCM encryptor for PII identifier columns; applied by an EF
        // value converter on UserProfile (SimfAppDbContext.OnModelCreating).
        services.AddSingleton<SIMF.Application.Abstractions.IPiiEncryptor, AesGcmPiiEncryptor>();
        // A6-18 — upload malware scanner (EICAR default; swap for ClamAV/Defender).
        // A6-18 (NCA) / D-568 — the malware-scan engine. "ClamAV" wires the real
        // clamd daemon (production); anything else keeps the built-in EICAR
        // detector. The centralized file pipeline runs whichever is registered
        // fail-closed in Production (D-494).
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

        // D-568 — the centralized file store: one envelope cipher + one storage
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
        // so a slow mail server never blocks a request (SIMF-SAD-001 A.2).
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailBackgroundService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
