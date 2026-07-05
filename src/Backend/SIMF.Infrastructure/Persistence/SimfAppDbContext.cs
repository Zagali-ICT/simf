using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SIMF.Application.Abstractions;
using SIMF.Domain.AccessControl;
using SIMF.Domain.Ai;
using SIMF.Domain.Archive;
using SIMF.Domain.Auditing;
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
/// <see cref="SimfIdentityDbContext"/>'s <c>SIMF_Identity</c> database (D-157,
/// superseding the earlier one-shared-DB design C-1). There is no cross-database
/// relation/FK and no duplicated data: a reference to an Identity user is a bare
/// <c>Guid</c> resolved on read (see the Data/Identity separation rule).
/// </summary>
public class SimfAppDbContext(DbContextOptions<SimfAppDbContext> options, IPiiEncryptor pii)
    : DbContext(options)
{
    private readonly IPiiEncryptor _pii = pii;

    /// <summary>The operation log — the durable audit trail (SIMF-FDS-001 section 9).</summary>
    public DbSet<OperationLogEntry> OperationLog => Set<OperationLogEntry>();

    /// <summary>D-109: row-audit trail for changes against this DbContext.</summary>
    public DbSet<RowAudit> RowAudits => Set<RowAudit>();

    /// <summary>D-134 Sprint B (D-135 freeze-lift) — programme themes / pillars.</summary>
    public DbSet<Theme> Themes => Set<Theme>();

    /// <summary>D-134 Sprint B (D-135) — venue halls.</summary>
    public DbSet<Hall> Halls => Set<Hall>();

    /// <summary>D-151 (D-135) — programme speakers.</summary>
    public DbSet<Speaker> Speakers => Set<Speaker>();

    /// <summary>P2.3 — D-228 (FR-407): speaker presentation files.</summary>
    public DbSet<SpeakerPresentation> SpeakerPresentations => Set<SpeakerPresentation>();

    /// <summary>D-452 — forum programme days (date + bilingual title + logo).</summary>
    public DbSet<ProgrammeDay> ProgrammeDays => Set<ProgrammeDay>();

    /// <summary>D-165 (gap doc G3, PDF §2.9) — scheduled run-of-show talks.</summary>
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionSpeaker> SessionSpeakers => Set<SessionSpeaker>();
    public DbSet<SessionTheme> SessionThemes => Set<SessionTheme>();
    // P4.1 — D-237: AI session summary / محضر (one per session).
    public DbSet<SessionSummary> SessionSummaries => Set<SessionSummary>();
    // P5.1 — D-241: hall arrival / attendance (GPS geofence or QR door scan).
    public DbSet<HallAttendance> HallAttendances => Set<HallAttendance>();
    // B9b — D-226: dynamic session-category lookup (FDS-004 §5.4).
    public DbSet<SessionCategory> SessionCategories => Set<SessionCategory>();

    /// <summary>P2.4 — D-229 (FDS-012 §5.5): platform system settings.</summary>
    public DbSet<SIMF.Domain.Configuration.SystemSetting> SystemSettings =>
        Set<SIMF.Domain.Configuration.SystemSetting>();

    /// <summary>P2.5 — D-230 (FR-605): 2D venue-map nodes.</summary>
    public DbSet<SIMF.Domain.Venue.VenueMapNode> VenueMapNodes =>
        Set<SIMF.Domain.Venue.VenueMapNode>();

    /// <summary>D-166 (gap doc G4, PDF §2.3) — registration open/close gate.</summary>
    public DbSet<RegistrationGate> RegistrationGate => Set<RegistrationGate>();

    /// <summary>D-166 (gap doc G4, PDF §2.4) — archive visibility switch.</summary>
    public DbSet<ArchiveVisibility> ArchiveVisibility => Set<ArchiveVisibility>();

    /// <summary>D-151 — country lookup (ISO 3166-1 numeric Id; admin-managed
    /// CRUD; seeded with ~56 priority countries on first migration).</summary>
    public DbSet<Country> Countries => Set<Country>();

    /// <summary>D-148 (D-135) — venue access gates.</summary>
    public DbSet<Gate> Gates => Set<Gate>();

    /// <summary>D-148 — per-gate allowed profile types.</summary>
    public DbSet<GateProfileTypeAllow> GateProfileTypeAllows => Set<GateProfileTypeAllow>();

    /// <summary>D-148 — operator-to-gate assignments.</summary>
    public DbSet<GateAssignment> GateAssignments => Set<GateAssignment>();

    /// <summary>D-148 — append-only scan log.</summary>
    public DbSet<GateScan> GateScans => Set<GateScan>();

    /// <summary>D-148 — 24h idempotency replay store.</summary>
    public DbSet<ScanIdempotency> ScanIdempotencies => Set<ScanIdempotency>();

    /// <summary>D-167 — visitor / other-user profile (was on Identity DB
    /// until D-167; UserId is now a logical FK to <c>SimfUser.Id</c>).</summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>D-167 — admin-managed lookup of profile subtypes
    /// (Visitor tiers + Other partner kinds).</summary>
    public DbSet<UserProfileType> ProfileTypes => Set<UserProfileType>();

    /// <summary>D-167 — admin-managed lookup of visitor interests.</summary>
    public DbSet<UserInterest> Interests => Set<UserInterest>();

    /// <summary>D-168 (gap doc G5, PDF §2.7.3) — public-relations invitations.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>D-169 (gap doc G6, PDF §2.7.2) — audience-submitted session questions.</summary>
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();

    /// <summary>D-169 (gap doc G6) — per-session moderator grants.</summary>
    public DbSet<SessionModerator> SessionModerators => Set<SessionModerator>();

    /// <summary>D-173 (gap doc G8, PDF §1) — editable public content
    /// blocks (welcome message, page copy, labels).</summary>
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    /// <summary>D-173 (gap doc G8, PDF §1) — time-windowed banners /
    /// announcements.</summary>
    public DbSet<Banner> Banners => Set<Banner>();

    /// <summary>D-269 (Mockup page 20) — attendee meeting requests TO a speaker
    /// (gated by Speaker.AllowsMeetingRequests). The session-scoped MeetingRequest
    /// feature (D-174) was removed in D-278.</summary>
    public DbSet<SpeakerMeetingRequest> SpeakerMeetingRequests => Set<SpeakerMeetingRequest>();

    // D-474 (#11, Group G) — speaker availability windows for the VIP-meeting slots.
    public DbSet<SpeakerAvailabilityWindow> SpeakerAvailabilityWindows => Set<SpeakerAvailabilityWindow>();

    // D-478 (#11, Group G phase 2) — delegation↔delegation (G2G) meeting requests.
    public DbSet<DelegationMeetingRequest> DelegationMeetingRequests => Set<DelegationMeetingRequest>();

    // D-500 (Wave 5, الطلبات 1408:9726) — the two new standalone request types
    // surfaced in the unified requests feed: participation-document + badge-update.
    public DbSet<SIMF.Domain.Requests.ParticipationDocumentRequest> ParticipationDocumentRequests =>
        Set<SIMF.Domain.Requests.ParticipationDocumentRequest>();
    public DbSet<SIMF.Domain.Requests.BadgeUpdateRequest> BadgeUpdateRequests =>
        Set<SIMF.Domain.Requests.BadgeUpdateRequest>();

    /// <summary>D-175 (gap doc G11, Mockup page 7) — per-hall seat
    /// grid layout (rows + seats-per-row). Optional 1:1 with Hall.</summary>
    public DbSet<HallSeatLayout> HallSeatLayouts => Set<HallSeatLayout>();

    /// <summary>D-175 (gap doc G11, Mockup page 7) — per-session
    /// seat reservations (visitor self-pick + random + admin row
    /// blocks). Released rows stay for audit.</summary>
    public DbSet<SeatReservation> SeatReservations => Set<SeatReservation>();

    /// <summary>D-176 (gap doc G12) — centralised AI prompt catalogue.
    /// Editable from the CP at runtime; one row per logical key.</summary>
    public DbSet<AiPrompt> AiPrompts => Set<AiPrompt>();

    /// <summary>D-188 (D-181 carry-over) — append-only snapshot table
    /// for AiPrompt edits. One row per (AiPromptId, Version) captured
    /// BEFORE the version bump that produced it.</summary>
    public DbSet<AiPromptHistory> AiPromptHistory => Set<AiPromptHistory>();

    /// <summary>D-176 (gap doc G12) — append-only telemetry log of
    /// every AI invocation (success or failure).</summary>
    public DbSet<AiInvocation> AiInvocations => Set<AiInvocation>();

    // D-199 — event modules (freeze lift): media coverage, exhibition,
    // sponsors, archive editions, ratings.
    public DbSet<News> News => Set<News>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<MediaPartner> MediaPartners => Set<MediaPartner>();
    public DbSet<Booth> Booths => Set<Booth>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<ArchiveEdition> ArchiveEditions => Set<ArchiveEdition>();
    // D-432 — owned snapshot children of an archive edition (gallery / session
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

    // D-202 — exhibitors + the accounts provisioned under them (additive
    // tables; account link is a logical Guid FK to SimfUser on the Identity DB
    // — no cross-DB navigation).
    public DbSet<Exhibitor> Exhibitors => Set<Exhibitor>();
    public DbSet<ExhibitorMembership> ExhibitorMemberships => Set<ExhibitorMembership>();

    // B3 (D-220) — Saudi-companies lookup, bulk-loaded from a government Excel
    // sheet; the visitor الجهة (UserProfile.OrganisationId) picker reads from it.
    public DbSet<Organisation> Organisations => Set<Organisation>();

    // Administrative-regions lookup (the 13 official Saudi regions). Additive
    // reference table under the D-219 freeze-lift; the app reads it for the
    // region picker. Seeded from SaudiRegions in every environment.
    public DbSet<Region> Regions => Set<Region>();

    // SIMF-FDS-014 — D-260: shared, de-duplicated contact directory referenced by
    // Company / Sponsor / MediaPartner / Speaker / Booth (nullable ContactId FK).
    public DbSet<Contact> Contacts => Set<Contact>();

    // SIMF-FDS-014 — D-284 (Track 2): visitor-to-visitor contact sharing. Both
    // tables hold bare-Guid logical FKs to SimfUser.Id (Identity DB) — no DB FK.
    public DbSet<VisitorShareToken> VisitorShareTokens => Set<VisitorShareToken>();
    public DbSet<SavedContact> SavedContacts => Set<SavedContact>();

    // D-426 — exhibitor ("Other") lead capture: visitors scanned at a booth.
    // Bare-Guid logical FKs to SimfUser.Id (Identity DB) — no DB FK.
    public DbSet<ExhibitorVisitorScan> ExhibitorVisitorScans => Set<ExhibitorVisitorScan>();

    // P2.1 (D-211) — two-level FAQ: groups own ordered question/answer entries.
    public DbSet<FaqGroup> FaqGroups => Set<FaqGroup>();
    public DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();

    // "تواصل معنا / Contact us" inbox (Figma 1388:7567) — app form submissions
    // triaged in the Control Panel. Additive table; submitter is a bare Guid.
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();

    // Session favourites (المفضلة) — the heart toggle on the session-summaries
    // (1388:8392) + my-sessions (1388:9067) screens. One row per (user, session).
    public DbSet<SessionFavourite> SessionFavourites => Set<SessionFavourite>();

    // D-495 — the singleton Organization / About profile + its two child lists
    // (the edition-generic forum config: name/dates/status/social/contact/about).
    public DbSet<OrganizationProfile> OrganizationProfile => Set<OrganizationProfile>();
    public DbSet<OrganizationAboutItem> OrganizationAboutItems => Set<OrganizationAboutItem>();
    public DbSet<OrganizationDetail> OrganizationDetails => Set<OrganizationDetail>();

    // B6 (D-224) — visitor-to-visitor networking connections (request/accept).
    public DbSet<Connection> Connections => Set<Connection>();

    // SIMF-FDS-013 — D-248: flexible hall allocation + admin-arranged B2B/B2C
    // business meetings (meeting tables, hall allocations, meetings + participants).
    public DbSet<MeetingTable> MeetingTables => Set<MeetingTable>();
    public DbSet<HallAllocation> HallAllocations => Set<HallAllocation>();
    public DbSet<BusinessMeeting> BusinessMeetings => Set<BusinessMeeting>();
    public DbSet<BusinessMeetingParticipant> BusinessMeetingParticipants =>
        Set<BusinessMeetingParticipant>();

    // D-568 — the single, unified file store: ONE table for every uploaded or
    // linked file (avatar, ID document, VIP photo, media gallery, speaker photo /
    // presentation, session recording, all logos, news / archive / banner images).
    // Replaces the seven bespoke filesystem stores + the D-357 Asset table. Bytes
    // live out-of-row (D-90); owner is a bare polymorphic Guid (D-157).
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SimfAppDbContext).Assembly,
            type => type.Namespace == "SIMF.Infrastructure.Persistence.Configurations.App");

        // A2-10 (NCA Secure App-Dev Standard) — encrypt the most sensitive PII
        // identifier columns at rest with AES-GCM (IPiiEncryptor). Applied here
        // (after the entity configurations) because the converter needs the
        // injected encryptor; the widened length (256) holds the enc:1: blob.
        // These columns have no index/unique/equality-query dependency, so
        // randomized encryption is safe (verified). Reads of legacy plaintext
        // rows are returned unchanged until they are next written.
        var piiConverter = new ValueConverter<string?, string?>(
            value => _pii.Encrypt(value),
            value => _pii.Decrypt(value));
        modelBuilder.Entity<SIMF.Domain.Profiles.UserProfile>(entity =>
        {
            foreach (var property in new[]
            {
                nameof(SIMF.Domain.Profiles.UserProfile.NationalId),
                nameof(SIMF.Domain.Profiles.UserProfile.IqamaNumber),
                nameof(SIMF.Domain.Profiles.UserProfile.PassportNumber),
                nameof(SIMF.Domain.Profiles.UserProfile.SaudiMobile),
                nameof(SIMF.Domain.Profiles.UserProfile.InternationalMobile),
            })
            {
                entity.Property(property).HasConversion(piiConverter).HasMaxLength(256);
            }
        });
    }
}
