using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SIMF.Application.Abstractions;
using SIMF.Domain.AccessControl;
using SIMF.Domain.Ai;
using SIMF.Domain.Archive;
using SIMF.Domain.Auditing;
using SIMF.Domain.Badges;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Cms;
using SIMF.Domain.Common;
using SIMF.Domain.Contacts;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Faq;
using SIMF.Domain.Feedback;
using SIMF.Domain.Files;
using SIMF.Domain.Media;
using SIMF.Domain.Networking;
using SIMF.Domain.Operations;
using SIMF.Domain.Organisations;
using SIMF.Domain.Organization;
using SIMF.Domain.Support;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.PublicRelations;
using SIMF.Domain.Regions;
using SIMF.Domain.SeatReservations;
using SIMF.Domain.SessionQuestions;
using SIMF.Domain.Sponsors;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// The application database context — the SIMF business entities. It lives in
/// its own **physically separate** database (<c>SIMF_App</c>), distinct from
/// <see cref="SimfIdentityDbContext"/>'s <c>SIMF_Identity</c> database. That
/// separation superseded an earlier one-shared-DB design. There is no
/// cross-database relation/FK and no duplicated data: a reference to an Identity
/// user is a bare <c>Guid</c> resolved on read (see the Data/Identity separation
/// rule).
/// </summary>
public class SimfAppDbContext(DbContextOptions<SimfAppDbContext> options, IPiiEncryptor pii)
    : DbContext(options)
{
    private readonly IPiiEncryptor _pii = pii;

    /// <summary>The operation log — the durable audit trail.</summary>
    public DbSet<OperationLogEntry> OperationLog => Set<OperationLogEntry>();

    /// <summary>Row-audit trail for changes against this DbContext.</summary>
    public DbSet<RowAudit> RowAudits => Set<RowAudit>();

    /// <summary>Programme themes / pillars.</summary>
    public DbSet<Theme> Themes => Set<Theme>();

    /// <summary>Venue halls.</summary>
    public DbSet<Hall> Halls => Set<Hall>();

    /// <summary>Programme speakers.</summary>
    public DbSet<Speaker> Speakers => Set<Speaker>();

    /// <summary>Speaker presentation files.</summary>
    public DbSet<SpeakerPresentation> SpeakerPresentations => Set<SpeakerPresentation>();

    /// <summary>Forum programme days (date + bilingual title + logo).</summary>
    public DbSet<ProgrammeDay> ProgrammeDays => Set<ProgrammeDay>();

    /// <summary>Scheduled run-of-show talks.</summary>
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionSpeaker> SessionSpeakers => Set<SessionSpeaker>();
    public DbSet<SessionTheme> SessionThemes => Set<SessionTheme>();
    // Session key-outcome bullets ("أبرز المخرجات" on the public detail page).
    public DbSet<SessionOutcome> SessionOutcomes => Set<SessionOutcome>();
    // AI session summary / محضر (one per session).
    public DbSet<SessionSummary> SessionSummaries => Set<SessionSummary>();
    // Hall arrival / attendance (GPS geofence or QR door scan).
    public DbSet<HallAttendance> HallAttendances => Set<HallAttendance>();
    // Periodic device-position samples — the capture path behind the
    // dwell-per-hall aggregation and the route projection. Inert until a hall is
    // given a geofence boundary.
    public DbSet<DevicePositionPing> DevicePositionPings => Set<DevicePositionPing>();
    // Dynamic session-category lookup.
    public DbSet<SessionCategory> SessionCategories => Set<SessionCategory>();

    /// <summary>Platform system settings.</summary>
    public DbSet<SIMF.Domain.Configuration.SystemSetting> SystemSettings =>
        Set<SIMF.Domain.Configuration.SystemSetting>();

    /// <summary>2D venue-map nodes.</summary>
    public DbSet<SIMF.Domain.Venue.VenueMapNode> VenueMapNodes =>
        Set<SIMF.Domain.Venue.VenueMapNode>();

    /// <summary>Registration open/close gate.</summary>
    public DbSet<RegistrationGate> RegistrationGate => Set<RegistrationGate>();

    /// <summary>Archive visibility switch.</summary>
    public DbSet<ArchiveVisibility> ArchiveVisibility => Set<ArchiveVisibility>();

    /// <summary>The year the forum is currently running.</summary>
    public DbSet<SIMF.Domain.Editions.EventEdition> EventEdition =>
        Set<SIMF.Domain.Editions.EventEdition>();

    /// <summary>Country lookup (ISO 3166-1 numeric Id; admin-managed
    /// CRUD; seeded with ~56 priority countries on first migration).</summary>
    public DbSet<Country> Countries => Set<Country>();

    /// <summary>Venue access gates.</summary>
    public DbSet<Gate> Gates => Set<Gate>();

    /// <summary>Per-gate allowed profile types.</summary>
    public DbSet<GateProfileTypeAllow> GateProfileTypeAllows => Set<GateProfileTypeAllow>();

    /// <summary>Operator-to-gate assignments.</summary>
    public DbSet<GateAssignment> GateAssignments => Set<GateAssignment>();

    /// <summary>Append-only scan log.</summary>
    public DbSet<GateScan> GateScans => Set<GateScan>();

    /// <summary>24h idempotency replay store.</summary>
    public DbSet<ScanIdempotency> ScanIdempotencies => Set<ScanIdempotency>();

    /// <summary>Visitor / other-user profile. It used to live on the Identity DB;
    /// UserId is now a logical FK to <c>SimfUser.Id</c>.</summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>Admin-managed lookup of profile subtypes
    /// (Visitor tiers + Other partner kinds).</summary>
    public DbSet<UserProfileType> ProfileTypes => Set<UserProfileType>();

    /// <summary>Admin-managed lookup of visitor interests.</summary>
    public DbSet<UserInterest> Interests => Set<UserInterest>();

    /// <summary>Public-relations invitations.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Audience-submitted session questions.</summary>
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();

    /// <summary>Per-session moderator grants.</summary>
    public DbSet<SessionModerator> SessionModerators => Set<SessionModerator>();

    /// <summary>Editable public content blocks (welcome message, page copy,
    /// labels).</summary>
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    /// <summary>Time-windowed banners / announcements.</summary>
    public DbSet<Banner> Banners => Set<Banner>();

    /// <summary>Attendee meeting requests TO a speaker (gated by
    /// Speaker.AllowsMeetingRequests). An earlier session-scoped MeetingRequest
    /// feature was removed.</summary>
    public DbSet<SpeakerMeetingRequest> SpeakerMeetingRequests => Set<SpeakerMeetingRequest>();

    // Speaker availability windows for the VIP-meeting slots.
    public DbSet<SpeakerAvailabilityWindow> SpeakerAvailabilityWindows => Set<SpeakerAvailabilityWindow>();

    // Hall availability windows (the "hall time" for business meetings);
    // symmetric with SpeakerAvailabilityWindows.
    public DbSet<HallAvailabilityWindow> HallAvailabilityWindows => Set<HallAvailabilityWindow>();

    // Delegation↔delegation (G2G) meeting requests.
    public DbSet<DelegationMeetingRequest> DelegationMeetingRequests => Set<DelegationMeetingRequest>();

    // Bi-Meeting rework — delegation availability windows (parity with
    // SpeakerAvailabilityWindows) so a delegation meeting can be booked on real time.
    public DbSet<DelegationAvailabilityWindow> DelegationAvailabilityWindows => Set<DelegationAvailabilityWindow>();

    // The two standalone request types (الطلبات) surfaced in the unified requests
    // feed: participation-document + badge-update.
    public DbSet<SIMF.Domain.Requests.ParticipationDocumentRequest> ParticipationDocumentRequests =>
        Set<SIMF.Domain.Requests.ParticipationDocumentRequest>();
    public DbSet<SIMF.Domain.Requests.BadgeUpdateRequest> BadgeUpdateRequests =>
        Set<SIMF.Domain.Requests.BadgeUpdateRequest>();

    /// <summary>Per-hall seat grid layout (rows + seats-per-row). Optional 1:1
    /// with Hall.</summary>
    public DbSet<HallSeatLayout> HallSeatLayouts => Set<HallSeatLayout>();

    /// <summary>Per-session seat reservations (visitor self-pick + random +
    /// admin row blocks). Released rows stay for audit.</summary>
    public DbSet<SeatReservation> SeatReservations => Set<SeatReservation>();

    public DbSet<SIMF.Domain.Notifications.NotificationBroadcast> NotificationBroadcasts =>
        Set<SIMF.Domain.Notifications.NotificationBroadcast>();

    /// <summary>Centralised AI prompt catalogue.
    /// Editable from the CP at runtime; one row per logical key.</summary>
    public DbSet<AiPrompt> AiPrompts => Set<AiPrompt>();

    /// <summary>Per-user AI-assistant chat history — the persisted conversation
    /// plus the assistant's short-term memory. Keyed by the bare user Guid, since
    /// there is no cross-DB FK to Identity.</summary>
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

    /// <summary>Append-only snapshot table for AiPrompt edits. One row per
    /// (AiPromptId, Version) captured BEFORE the version bump that produced
    /// it.</summary>
    public DbSet<AiPromptHistory> AiPromptHistory => Set<AiPromptHistory>();

    /// <summary>Append-only telemetry log of
    /// every AI invocation (success or failure).</summary>
    public DbSet<AiInvocation> AiInvocations => Set<AiInvocation>();

    /// <summary>Admin overrides for the transactional identity emails
    /// (sign-in OTP, verification, password reset, activation, biometric
    /// step-up). Editable from the CP at runtime; one row per type. Empty until
    /// an admin customises a template (the resolver falls back to code
    /// defaults).</summary>
    public DbSet<SIMF.Domain.Email.EmailTemplate> EmailTemplates =>
        Set<SIMF.Domain.Email.EmailTemplate>();

    // Event modules — additive tables: media coverage, exhibition, sponsors,
    // archive editions, ratings.
    public DbSet<News> News => Set<News>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<MediaPartner> MediaPartners => Set<MediaPartner>();
    public DbSet<Booth> Booths => Set<Booth>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<ArchiveEdition> ArchiveEditions => Set<ArchiveEdition>();
    // Owned snapshot children of an archive edition (gallery / session
    // titles / past speakers).
    public DbSet<ArchiveMediaItem> ArchiveMediaItems => Set<ArchiveMediaItem>();
    public DbSet<ArchiveSessionTitle> ArchiveSessionTitles => Set<ArchiveSessionTitle>();
    public DbSet<ArchivePastSpeaker> ArchivePastSpeakers => Set<ArchivePastSpeaker>();
    // Dynamic, config-driven ratings — admin defines types/groups/questions,
    // attendees submit responses with per-question answers (replaces the old
    // fixed single-row Rating model).
    public DbSet<RatingType> RatingTypes => Set<RatingType>();
    public DbSet<RatingQuestionGroup> RatingQuestionGroups => Set<RatingQuestionGroup>();
    public DbSet<RatingQuestion> RatingQuestions => Set<RatingQuestion>();
    public DbSet<RatingResponse> RatingResponses => Set<RatingResponse>();
    public DbSet<RatingAnswer> RatingAnswers => Set<RatingAnswer>();

    // Exhibitors + the accounts provisioned under them (additive
    // tables; account link is a logical Guid FK to SimfUser on the Identity DB
    // — no cross-DB navigation).
    public DbSet<Exhibitor> Exhibitors => Set<Exhibitor>();
    public DbSet<ExhibitorMembership> ExhibitorMemberships => Set<ExhibitorMembership>();

    // Saudi-companies lookup, bulk-loaded from a government Excel
    // sheet; the visitor الجهة (UserProfile.OrganisationId) picker reads from it.
    public DbSet<Organisation> Organisations => Set<Organisation>();

    // Administrative-regions lookup (the 13 official Saudi regions) — an
    // additive reference table. The app reads it for the region picker. Seeded
    // from SaudiRegions in every environment.
    public DbSet<Region> Regions => Set<Region>();

    // Visitor-to-visitor contact sharing. Both tables hold bare-Guid logical FKs
    // to SimfUser.Id (Identity DB) — no DB FK.
    public DbSet<VisitorShareToken> VisitorShareTokens => Set<VisitorShareToken>();
    public DbSet<SavedContact> SavedContacts => Set<SavedContact>();

    // Exhibitor ("Other") lead capture: visitors scanned at a booth.
    // Bare-Guid logical FKs to SimfUser.Id (Identity DB) — no DB FK.
    public DbSet<ExhibitorVisitorScan> ExhibitorVisitorScans => Set<ExhibitorVisitorScan>();

    // Two-level FAQ: groups own ordered question/answer entries.
    public DbSet<FaqGroup> FaqGroups => Set<FaqGroup>();
    public DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();

    // "تواصل معنا / Contact us" inbox — app form submissions triaged in the
    // Control Panel. An additive table; the submitter is a bare Guid.
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();

    // Session favourites (المفضلة) — the heart toggle on the session-summaries
    // and my-sessions screens. One row per (user, session).
    public DbSet<SessionFavourite> SessionFavourites => Set<SessionFavourite>();

    // The singleton Organization / About profile + its two child lists
    // (the edition-generic forum config: name/dates/status/social/contact/about).
    public DbSet<OrganizationProfile> OrganizationProfile => Set<OrganizationProfile>();
    public DbSet<OrganizationAboutItem> OrganizationAboutItems => Set<OrganizationAboutItem>();
    public DbSet<OrganizationDetail> OrganizationDetails => Set<OrganizationDetail>();

    // Visitor-to-visitor networking connections (request/accept).
    public DbSet<Connection> Connections => Set<Connection>();

    // Flexible hall allocation + admin-arranged B2B/B2C business meetings
    // (meeting tables, hall allocations, meetings + participants).
    public DbSet<MeetingTable> MeetingTables => Set<MeetingTable>();
    public DbSet<HallAllocation> HallAllocations => Set<HallAllocation>();
    public DbSet<BusinessMeeting> BusinessMeetings => Set<BusinessMeeting>();
    public DbSet<BusinessMeetingParticipant> BusinessMeetingParticipants =>
        Set<BusinessMeetingParticipant>();

    // Single-use speaker double-opt-in action-link tokens (Approve / Reject).
    public DbSet<MeetingActionToken> MeetingActionTokens => Set<MeetingActionToken>();

    // Single-use delegation confirm tokens behind the "please confirm" email
    // link sent to target-delegation members on Approve.
    public DbSet<DelegationMeetingActionToken> DelegationMeetingActionTokens =>
        Set<DelegationMeetingActionToken>();

    // The single, unified file store: ONE table for every uploaded or
    // linked file (avatar, ID document, VIP photo, media gallery, speaker photo /
    // presentation, session recording, all logos, news / archive / banner images).
    // Replaces the seven bespoke filesystem stores and the older Asset table.
    // Bytes live out-of-row; owner is a bare polymorphic Guid.
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    // Bulk-badge generation runs, so a minted set of
    // placeholder badges can be re-emailed / revoked as a unit. Each member
    // UserProfile carries a nullable BadgeBatchId back-reference (intra-App FK).
    public DbSet<BadgeBatch> BadgeBatches => Set<BadgeBatch>();

    // What each order holds, one row per profile type. Replaced the rendered
    // "VIP × 3 + Normal × 2" string on BadgeBatches, which could not be queried
    // and froze the tier name at mint time; the label is composed on read from
    // these rows joined to the live profile type.
    public DbSet<BadgeBatchItem> BadgeBatchItems => Set<BadgeBatchItem>();


    /// <summary>Case- and accent-insensitive Arabic collation, applied to every
    /// <c>*Arabic</c> string column.
    ///
    /// <para>What it actually folds, measured against the engine rather than
    /// assumed: the alef maksura and the yeh become one letter, so
    /// <c>مصطفى</c> and <c>مصطفي</c> match for search,
    /// for equality and inside the unique indexes. That is one of the two
    /// commonest Arabic spelling splits and it was silently costing rows before.</para>
    ///
    /// <para>What it does NOT fold, which is worth knowing before promising it to
    /// anyone: a PRECOMPOSED alef-hamza is not equal to a bare alef, so
    /// <c>أحمد</c> is still not found by a search for
    /// <c>احمد</c>. Accent-insensitivity discards a secondary weight,
    /// and only the decomposed sequence U+0627 U+0654 carries one; the
    /// precomposed U+0623 has a primary weight of its own and is simply a
    /// different letter to SQL Server. Every Arabic keyboard emits the
    /// precomposed form. Folding those would need a normalised shadow of the text
    /// written on the way in and searched instead of the display column, which is
    /// a schema change - normalising only the needle cannot change the stored
    /// haystack.</para></summary>
    private const string ArabicCollation = "Arabic_CI_AI";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfAppDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations.App");

        // The concurrency-safe issuer for the human-quotable
        // registration reference (SIMF-<year>-<8-digit sequence>), read raw in
        // UserProfileRepository.NextRegistrationReferenceAsync via NEXT VALUE FOR.
        // Declared on the model so `Migrate()` creates it on every fresh DB
        // (CI test factory + a re-seeded prod). It was previously created only
        // by hand on the dev DB and never captured, so migration-built DBs
        // (prod after the InitialMigration squash) lacked it and every
        // first-time profile save threw SqlException 208 → a 500.
        modelBuilder.HasSequence<long>("RegistrationReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);

        // A2-10 (NCA Secure App-Dev Standard) — encrypt the most sensitive PII
        // identifier columns at rest with AES-GCM (IPiiEncryptor). Applied here
        // (after the entity configurations) because the converter needs the
        // injected encryptor; the widened length (256) holds the enc:1: blob.
        // These columns have no index/unique/equality-query dependency, so
        // randomized encryption is safe (verified). Reads of legacy plaintext
        // rows are returned unchanged until they are next written.

        // Arabic search has been accent-SENSITIVE since the beginning, because no
        // collation was ever set and both databases therefore run the SQL Server
        // instance default (a *_CI_AS). Under it "مصطفى" does not match "مصطفي" - the
        // alef maksura and the yeh are different characters, and the same name is
        // routinely typed both ways by the same person. Every admin grid search box
        // and every public search over a bilingual column was quietly missing rows,
        // and there is NO fix in C# - normalising the needle cannot change how the
        // server compares it to the stored haystack.
        //
        // Measured, not assumed, and narrower than it first looks. Arabic_CI_AI
        // folds maksura/yeh; it does NOT fold a PRECOMPOSED alef-hamza onto a bare
        // alef, so "أحمد" is still not found by searching "احمد". Accent-
        // insensitivity discards a secondary weight and only the decomposed
        // sequence U+0627 U+0654 has one, while the precomposed U+0623 carries a
        // primary weight of its own - and a precomposed alef is what every Arabic
        // keyboard produces. Closing that second gap needs a normalised shadow
        // column written on the way in, not a collation.
        //
        // Accent-INSENSITIVE collation is the fix, applied per column rather than
        // per database so it lands on exactly the text it is meant for: every
        // string property whose name ends "Arabic". The bilingual convention across
        // this model is a pair (Name, NameArabic), so the suffix IS the set, and
        // keying on it means a column added later inherits the collation without
        // anyone remembering to ask for it.
        //
        // What this changes besides search: equality does too, so a duplicate probe
        // over an Arabic name now treats the two spellings as the same name. That is
        // the desired reading of "already exists" rather than a side effect. Exactly
        // one unique index covers an Arabic column - Sponsor (Tier, NameArabic)
        // filtered on IsActive - and every seeded Arabic literal was checked against
        // the fold before this landed: nothing collides.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string)
                    && property.Name.EndsWith("Arabic", StringComparison.Ordinal))
                {
                    property.SetCollation(ArabicCollation);
                }
            }
        }

        var piiConverter = new ValueConverter<string?, string?>(
            value => _pii.Encrypt(value),
            value => _pii.Decrypt(value));
        modelBuilder.Entity<SIMF.Domain.Profiles.UserProfile>(entity =>
        {
            foreach (var property in new[]
            {
                // The canonical number and the two columns it supersedes. All
                // three are the same fact, so all three carry the same
                // protection — leaving the new one in plaintext would have made
                // the collapse a PII regression.
                nameof(SIMF.Domain.Profiles.UserProfile.MobileNumber),
                nameof(SIMF.Domain.Profiles.UserProfile.SaudiMobile),
                nameof(SIMF.Domain.Profiles.UserProfile.InternationalMobile),
            })
            {
                entity.Property(property).HasConversion(piiConverter).HasMaxLength(256);
            }
        });

        // The identity-document numbers used to be three more names in the loop
        // above. They are now one column on their own entity, so they need their
        // own registration — the loop is keyed on UserProfile property NAMES, and
        // a different entity is invisible to it. A missed registration is SILENT: a
        // plaintext number reads back perfectly (Decrypt returns unmarked values
        // unchanged), so nothing fails and the PII simply sits in the clear.
        //
        // NumberHash is deliberately NOT here. It is the deterministic digest the
        // unique index keys off, and encrypting it under a random nonce would make
        // it a different value on every write.
        // Its own converter rather than the one above, because the column is
        // REQUIRED: a document row exists only when there is a number to put in
        // it. The nullable converter does not type-check against a non-nullable
        // property, and papering over that with the stringly-typed Property
        // overload would only hide the difference the column actually has.
        var requiredPiiConverter = new ValueConverter<string, string?>(
            value => _pii.Encrypt(value),
            value => _pii.Decrypt(value) ?? string.Empty);
        modelBuilder.Entity<SIMF.Domain.Profiles.ProfileIdentityDocument>()
            .Property(document => document.Number)
            .HasConversion(requiredPiiConverter)
            .HasMaxLength(256);

        // The saved AI transcript is the third column on this converter, and it is
        // here for a reason redaction could not cover. The turns were being stored
        // REDACTED, which stops a national id or a mobile number landing in the
        // clear but stops nothing else: the redactor matches known patterns, so a
        // free-text address, an employer, a medical detail or a name typed into the
        // chat was never a pattern and sat in plaintext in a table nothing prunes.
        // Encrypting the column protects all of it, and it also gives the visitor
        // their own words back - a reopened transcript no longer reads
        // "[REDACTED_EMAIL]" where the assistant helpfully quoted the event's own
        // contact address. Redaction still happens, but on the way OUT to the model
        // provider, which is the boundary it was always meant to guard.
        //
        // nvarchar(max), not a widened fixed length: the plaintext is already
        // capped at AiInputLimits.MaxInputValueLength and the base64 envelope is
        // about 1.4x that plus the marker, which does not fit the 4000-character
        // ceiling nvarchar allows. Nothing queries this column - no Contains, no
        // equality, no ORDER BY - so randomized encryption costs nothing here.
        modelBuilder.Entity<SIMF.Domain.Ai.AiChatMessage>()
            .Property(message => message.Content)
            .HasConversion(requiredPiiConverter)
            .HasColumnType("nvarchar(max)");
    }
}
