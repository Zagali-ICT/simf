using SIMF.Common.Enums;

namespace SIMF.Common;

/// <summary>
/// One entry in the permission catalogue — the metadata seeded into the
/// <c>Permission</c> table (SIMF-RPM-001 §8, SIMF-DAT-001 §5.1).
/// <para><paramref name="BaselineRoles"/> lists the built-in non-Administrator
/// roles that receive this permission as a seeded grant. Administrator is
/// never listed: it is resolved to the wildcard <see cref="PermissionCatalog.Wildcard"/>
/// at token-mint time and therefore holds every permission implicitly.</para>
/// </summary>
public sealed record PermissionDef(
    string Code,
    string Page,
    string Action,
    string DisplayName,
    IReadOnlyList<string> BaselineRoles);

/// <summary>
/// The complete page-and-action permission catalogue for the Control Panel
/// and admin API (SIMF-RPM-001 §8). This is the single source of truth:
/// <list type="bullet">
/// <item>endpoints gate with <see cref="PolicyFor"/>(code);</item>
/// <item>CP pages gate with <c>[RequirePermission(code)]</c>;</item>
/// <item>the side menu filters items by their <c>RequiredPermission</c>;</item>
/// <item>the seeder inserts one <c>Permission</c> row per <see cref="All"/> entry.</item>
/// </list>
/// <para>Codes follow the existing <c>Page.Action</c> convention (e.g.
/// <c>Sessions.Edit</c>). The six codes that pre-date this catalogue
/// (D-148 gate triad, D-168 PR/VIP triad) keep their exact strings,
/// pages, actions and display names so existing seeded rows and grants
/// match on re-seed.</para>
/// </summary>
public static class PermissionCatalog
{
    /// <summary>The wildcard permission value an Administrator carries in
    /// its <c>perm</c> claim. A permission check passes when the principal
    /// holds the requested code <b>or</b> this wildcard.</summary>
    public const string Wildcard = "*";

    /// <summary>The JWT / cookie claim type that carries permission codes.</summary>
    public const string ClaimType = "perm";

    /// <summary>The prefix that marks an authorization policy name as a
    /// permission policy (consumed by the dynamic policy provider).</summary>
    public const string PolicyPrefix = "perm:";

    /// <summary>Builds the authorization policy name for a permission code,
    /// e.g. <c>Sessions.Edit</c> → <c>perm:Sessions.Edit</c>. Used by API
    /// endpoints (<c>Policies(PermissionCatalog.PolicyFor(...))</c>).</summary>
    public static string PolicyFor(string code) => PolicyPrefix + code;

    /// <summary>True when an authorization policy name is a permission policy.</summary>
    public static bool IsPermissionPolicy(string policyName) =>
        policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal);

    /// <summary>Extracts the permission code from a permission policy name;
    /// <c>perm:Sessions.Edit</c> → <c>Sessions.Edit</c>.</summary>
    public static string CodeFromPolicy(string policyName) =>
        policyName[PolicyPrefix.Length..];

    // ── People & accounts ────────────────────────────────────────────────

    /// <summary>Admin (CP staff) accounts.</summary>
    public static class Admins
    {
        public const string View = "Admins.View";
        public const string Create = "Admins.Create";
        public const string Delete = "Admins.Delete";
        public const string Approve = "Admins.Approve";
        public const string Reject = "Admins.Reject";
        public const string Export = "Admins.Export";
        public const string Import = "Admins.Import";
        public const string ResetTwoFactor = "Admins.ResetTwoFactor";
        public const string AssignRoles = "Admins.AssignRoles";
    }

    /// <summary>"Other" (partner-side) accounts — Staff / Media / Sponsor.</summary>
    public static class Others
    {
        public const string View = "Others.View";
        public const string Create = "Others.Create";
        public const string Edit = "Others.Edit";
        public const string Delete = "Others.Delete";
        public const string Approve = "Others.Approve";
        public const string Reject = "Others.Reject";
        public const string Export = "Others.Export";
        public const string Import = "Others.Import";
        public const string RegisterOnsite = "Others.RegisterOnsite";
    }

    /// <summary>Visitor (audience) accounts.</summary>
    public static class Visitors
    {
        public const string View = "Visitors.View";
        public const string Create = "Visitors.Create";
        public const string Edit = "Visitors.Edit";
        public const string Delete = "Visitors.Delete";
        public const string Approve = "Visitors.Approve";
        public const string Reject = "Visitors.Reject";
        public const string Export = "Visitors.Export";
        public const string Import = "Visitors.Import";
        public const string RegisterOnsite = "Visitors.RegisterOnsite";
        // V-1 (D-429) — export the VVIP/VIP welcome roster (موج/Mawj) as the
        // API + CSV + Excel feed shared with the technical teams.
        public const string ExportVip = "Visitors.ExportVip";

        /// <summary>D-473 (#10) — bulk-generate placeholder badges by profile
        /// type + count (visitors or delegates).</summary>
        public const string BulkGenerate = "Visitors.BulkGenerate";

        /// <summary>D-758 (#10 Phase 2) — view the persisted bulk-badge batches.</summary>
        public const string ViewBatches = "Visitors.ViewBatches";

        /// <summary>Security review (2026-07-24) — the destructive batch actions
        /// (re-email the QR pack / revoke a batch = disable its accounts), split out
        /// of <see cref="ViewBatches"/> so read-only batch visibility can be granted
        /// without the power to re-email or revoke.</summary>
        public const string ManageBatches = "Visitors.ManageBatches";
    }

    /// <summary>Cross-scope account operations that span both the audience
    /// (Visitor) and partner (Other) desks.</summary>
    public static class Accounts
    {
        /// <summary>D-728 (owner item 9) — flip an existing account between the
        /// audience (Visitor) and partner (Other) scope by reassigning its
        /// profile type to one in the opposite scope. A privilege change
        /// (rolls the security stamp + revokes sessions), so it carries its own
        /// dedicated code rather than reusing Visitors.Edit / Others.Edit.</summary>
        public const string ChangeType = "Accounts.ChangeType";
    }

    /// <summary>Attendee roster + badge printing.</summary>
    public static class Attendees
    {
        public const string View = "Attendees.View";
        public const string PrintBag = "Attendees.PrintBag";

        // P1.6 — XLSX export of the (filtered) attendee roster.
        public const string Export = "Attendees.Export";
    }

    /// <summary>RBAC roles and their permission grants.</summary>
    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
        public const string AssignPermissions = "Roles.AssignPermissions";
        public const string Export = "Roles.Export";
        public const string Import = "Roles.Import";
    }

    /// <summary>Profile interests reference data.</summary>
    public static class Interests
    {
        public const string View = "Interests.View";
        public const string Create = "Interests.Create";
        public const string Edit = "Interests.Edit";
        public const string Delete = "Interests.Delete";
        public const string Export = "Interests.Export";
        public const string Import = "Interests.Import";
    }

    /// <summary>Countries reference data.</summary>
    public static class Countries
    {
        public const string View = "Countries.View";
        public const string Create = "Countries.Create";
        public const string Edit = "Countries.Edit";
        public const string Delete = "Countries.Delete";
        public const string Export = "Countries.Export";
        public const string Import = "Countries.Import";
    }

    /// <summary>Profile types (visitor + other) reference data.</summary>
    public static class ProfileTypes
    {
        public const string View = "ProfileTypes.View";
        public const string Create = "ProfileTypes.Create";
        public const string Edit = "ProfileTypes.Edit";
        public const string Delete = "ProfileTypes.Delete";
    }

    /// <summary>B3 (D-220) — Saudi-companies lookup, bulk-loaded from a
    /// government Excel sheet; the visitor الجهة picker reads from this
    /// reference table.</summary>
    public static class Organisations
    {
        public const string View = "Organisations.View";
        public const string Create = "Organisations.Create";
        public const string Edit = "Organisations.Edit";
        public const string Delete = "Organisations.Delete";
        public const string Import = "Organisations.Import";
        public const string Export = "Organisations.Export";
    }

    /// <summary>Administrative-regions lookup (the 13 official Saudi regions);
    /// the app region picker reads from this reference table.</summary>
    public static class Regions
    {
        public const string View = "Regions.View";
        public const string Create = "Regions.Create";
        public const string Edit = "Regions.Edit";
        public const string Delete = "Regions.Delete";
    }

    // ── Programme ────────────────────────────────────────────────────────

    /// <summary>B9b — D-226: dynamic session-category lookup (FDS-004 §5.4).</summary>
    public static class SessionCategories
    {
        public const string View = "SessionCategories.View";
        public const string Create = "SessionCategories.Create";
        public const string Edit = "SessionCategories.Edit";
        public const string Delete = "SessionCategories.Delete";
        public const string Export = "SessionCategories.Export";
        public const string Import = "SessionCategories.Import";
    }

    public static class Themes
    {
        public const string View = "Themes.View";
        public const string Create = "Themes.Create";
        public const string Edit = "Themes.Edit";
        public const string Delete = "Themes.Delete";
        public const string Export = "Themes.Export";
        public const string Import = "Themes.Import";
    }

    public static class Sessions
    {
        public const string View = "Sessions.View";
        public const string Create = "Sessions.Create";
        public const string Edit = "Sessions.Edit";
        public const string Delete = "Sessions.Delete";

        // P3.2 — D-231: drive the broadcast lifecycle (mark held / recorded,
        // publish + un-publish). Held by the Scientific Committee role.
        public const string Publish = "Sessions.Publish";
        public const string Export = "Sessions.Export";
        public const string Import = "Sessions.Import";
    }

    /// <summary>D-452 — the programme-days manager (date + bilingual title +
    /// logo) backing the app's "تفاصيل اليوم" day banner (Figma 883:2308).</summary>
    public static class ProgrammeDays
    {
        public const string View = "ProgrammeDays.View";
        public const string Create = "ProgrammeDays.Create";
        public const string Edit = "ProgrammeDays.Edit";
        public const string Delete = "ProgrammeDays.Delete";
        public const string Export = "ProgrammeDays.Export";
        public const string Import = "ProgrammeDays.Import";
    }

    public static class ProgrammeTimeline
    {
        public const string View = "ProgrammeTimeline.View";
    }

    public static class Halls
    {
        public const string View = "Halls.View";
        public const string Create = "Halls.Create";
        public const string Edit = "Halls.Edit";
        public const string Delete = "Halls.Delete";
        public const string Export = "Halls.Export";
        public const string Import = "Halls.Import";
    }

    /// <summary>Hall seat-layout editor.</summary>
    public static class SeatLayouts
    {
        public const string View = "SeatLayouts.View";
        public const string Edit = "SeatLayouts.Edit";

        /// <summary>B15 — remove a hall's seat layout entirely (convert the hall back
        /// to general admission). Separate from <see cref="Edit"/> because it is
        /// destructive: it discards the whole grid, not one row of it.</summary>
        public const string Delete = "SeatLayouts.Delete";
    }

    /// <summary>Per-session seat plan / reservations.</summary>
    public static class SeatPlans
    {
        public const string View = "SeatPlans.View";
        public const string Edit = "SeatPlans.Edit";
    }

    /// <summary>D-771 (owner 2026-07-26) — the on-ground SEATING DESK: staff help a
    /// guest find their seat (scan a badge → where do they sit; tap a seat → who
    /// sits there). This is an OPERATIONAL capability, not a Control-Panel page: it
    /// is granted to the app's Staff / Moderator roles via
    /// <see cref="OperationalPermissionsForAppRole"/> so a partner-side staff
    /// attendee reaches it without any admin RBAC role.</summary>
    public static class Seating
    {
        public const string Assist = "Seating.Assist";
    }

    /// <summary>#6/#17 (owner 2026-07-20) — the read-only booking monitor. There is
    /// no approval step (bookings auto-confirm) and no-shows are released by a
    /// background worker, so Approve/Reject were retired — only View + Export
    /// remain.</summary>
    public static class Bookings
    {
        public const string View = "Bookings.View";
        public const string Export = "Bookings.Export";
    }

    /// <summary>SIMF-FDS-013 (D-248) — meeting tables inside a Meeting/General hall
    /// (define / generate random-by-count / by row-column).</summary>
    public static class MeetingTables
    {
        public const string View = "MeetingTables.View";
        public const string Edit = "MeetingTables.Edit";
        public const string Export = "MeetingTables.Export";
    }

    /// <summary>SIMF-FDS-013 (D-248) — the flexible hall allocation layer
    /// (reserve whole / random-by-count / row-column over a time-slot).</summary>
    public static class HallAllocations
    {
        public const string View = "HallAllocations.View";
        public const string Edit = "HallAllocations.Edit";
    }

    /// <summary>QA A36 — the hall's meeting-time windows (D-715, FDS-013 §15
    /// GAP-1) and the free slots derived from them. Its own code because the
    /// resource is hall-scoped and read by BOTH meeting desks (speaker
    /// <i>and</i> delegation): gating it on <see cref="SpeakerMeetingRequests"/>
    /// locked a delegation-only or halls-only operator out of the windows every
    /// meeting Approve modal depends on. <c>View</c> reads the windows + free
    /// slots (grant it to any meeting-desk role); <c>Manage</c> creates and
    /// deletes windows.</summary>
    public static class HallAvailability
    {
        public const string View = "HallAvailability.View";
        public const string Manage = "HallAvailability.Manage";
    }

    /// <summary>SIMF-FDS-013 (D-248) — admin-arranged B2B/B2C business meetings.</summary>
    public static class BusinessMeetings
    {
        public const string View = "BusinessMeetings.View";
        public const string Schedule = "BusinessMeetings.Schedule";
        public const string Cancel = "BusinessMeetings.Cancel";
        public const string Export = "BusinessMeetings.Export";
    }

    public static class SpeakerMeetingRequests
    {
        public const string View = "SpeakerMeetingRequests.View";
        public const string Manage = "SpeakerMeetingRequests.Manage";
        public const string Export = "SpeakerMeetingRequests.Export";
    }

    /// <summary>D-478 (#11, Group G phase 2) — delegation↔delegation meeting
    /// requests review desk. <c>View</c> lists/opens; <c>Manage</c> accepts/rejects.</summary>
    public static class DelegationMeetings
    {
        public const string View = "DelegationMeetings.View";
        public const string Manage = "DelegationMeetings.Manage";
    }

    /// <summary>D-500 (Wave 5, الطلبات) — participation-document requests desk
    /// (طلب وثيقة المشاركة). <c>View</c> lists/opens; <c>Manage</c> accepts/rejects.</summary>
    public static class ParticipationDocumentRequests
    {
        public const string View = "ParticipationDocumentRequests.View";
        public const string Manage = "ParticipationDocumentRequests.Manage";
    }

    /// <summary>D-500 (Wave 5, الطلبات) — badge-update requests desk
    /// (طلب تحديث البادج). <c>View</c> lists/opens; <c>Manage</c> accepts/rejects.</summary>
    public static class BadgeUpdateRequests
    {
        public const string View = "BadgeUpdateRequests.View";
        public const string Manage = "BadgeUpdateRequests.Manage";
    }

    public static class Speakers
    {
        public const string View = "Speakers.View";
        public const string Create = "Speakers.Create";
        public const string Edit = "Speakers.Edit";
        public const string Delete = "Speakers.Delete";
        public const string Export = "Speakers.Export";
        public const string Import = "Speakers.Import";
    }

    /// <summary>D-357 — the centralised media-library page that manages every
    /// unified media <c>Asset</c> across all entities. <c>View</c> lists +
    /// previews; <c>Manage</c> deactivates / restores / edits links.</summary>
    public static class MediaLibrary
    {
        public const string View = "MediaLibrary.View";
        public const string Manage = "MediaLibrary.Manage";
    }

    /// <summary>Assigning moderators to sessions.</summary>
    public static class SessionModerators
    {
        public const string View = "SessionModerators.View";
        public const string Assign = "SessionModerators.Assign";
        public const string Revoke = "SessionModerators.Revoke";
        public const string Export = "SessionModerators.Export";
    }

    /// <summary>The live session moderation desk (Q&amp;A).</summary>
    public static class SessionModeration
    {
        public const string Moderate = "SessionModeration.Moderate";
    }

    /// <summary>P3.3 — D-212: the Scientific-Committee central Q&amp;A queue
    /// (stage 2 of the pipeline). The "Scientific Committee" is a role = a
    /// bundle of these + the programme codes, created by the owner in the
    /// grant editor (D-207/D-208) — not new infrastructure.</summary>
    public static class Questions
    {
        public const string View = "Questions.View";
        public const string Moderate = "Questions.Moderate";
        public const string Escalate = "Questions.Escalate";
        public const string Export = "Questions.Export";
    }

    /// <summary>P4.1 — D-238: the Scientific-Committee AI session-summary / محضر
    /// desk (Completion Programme §6.4.1, Mockup screen 34). View the desk +
    /// editor; Edit drafts (AI-generate or hand-write + save); Publish makes the
    /// محضر public to the app (and un-publishes).</summary>
    public static class SessionSummaries
    {
        public const string View = "SessionSummaries.View";
        public const string Edit = "SessionSummaries.Edit";
        public const string Publish = "SessionSummaries.Publish";
        public const string Export = "SessionSummaries.Export";

        /// <summary>D-472 (#9) — approve a submitted محضر (and send one back to
        /// draft); the approved محضر becomes readable by the session host/moderator.</summary>
        public const string Approve = "SessionSummaries.Approve";
    }

    /// <summary>P5.1d — D-244 (FDS-003 §5.4): the hall-door arrival console — an
    /// operator scans an attendee's badge QR to record hall arrival
    /// (<c>Method = QrScan</c>). View the console; Record a door movement.</summary>
    public static class HallArrivals
    {
        public const string View = "HallArrivals.View";

        /// <summary>FR-CHK-003 — record a hall door movement in EITHER direction:
        /// the arrival (check-in) and the departure (check-out) endpoints and both
        /// console buttons share this one code. The operator population is identical
        /// for the two directions, so they are deliberately not split.</summary>
        public const string Record = "HallArrivals.Record";
    }

    // ── Exhibition ───────────────────────────────────────────────────────

    public static class Exhibitors
    {
        public const string View = "Exhibitors.View";
        public const string Create = "Exhibitors.Create";
        public const string Edit = "Exhibitors.Edit";
        public const string Delete = "Exhibitors.Delete";
        public const string Export = "Exhibitors.Export";
        public const string Import = "Exhibitors.Import";
    }

    public static class Booths
    {
        public const string View = "Booths.View";
        public const string Create = "Booths.Create";
        public const string Edit = "Booths.Edit";
        public const string Delete = "Booths.Delete";
        public const string Export = "Booths.Export";
        public const string Import = "Booths.Import";
    }

    public static class Sponsors
    {
        public const string View = "Sponsors.View";
        public const string Create = "Sponsors.Create";
        public const string Edit = "Sponsors.Edit";
        public const string Delete = "Sponsors.Delete";
        public const string Export = "Sponsors.Export";
        public const string Import = "Sponsors.Import";
    }

    /// <summary>P2.5 (D-230) — 2D venue map editor (FR-605, FDS-006 §5.3).</summary>
    public static class VenueMap
    {
        public const string View = "VenueMap.View";
        public const string Create = "VenueMap.Create";
        public const string Edit = "VenueMap.Edit";
        public const string Delete = "VenueMap.Delete";
        public const string Export = "VenueMap.Export";
        public const string Import = "VenueMap.Import";
    }

    // ── Engagement ───────────────────────────────────────────────────────

    /// <summary>Ratings / feedback viewer.</summary>
    public static class Ratings
    {
        public const string View = "Ratings.View";
        public const string Export = "Ratings.Export";
    }

    /// <summary>Dynamic rating configuration — admin CRUD over rating types,
    /// question groups and questions.</summary>
    public static class RatingConfig
    {
        public const string View = "RatingConfig.View";
        public const string Create = "RatingConfig.Create";
        public const string Edit = "RatingConfig.Edit";
        public const string Delete = "RatingConfig.Delete";
    }

    // ── Knowledge ────────────────────────────────────────────────────────

    public static class AiPrompts
    {
        public const string View = "AiPrompts.View";
        public const string Create = "AiPrompts.Create";
        public const string Edit = "AiPrompts.Edit";
        public const string Delete = "AiPrompts.Delete";
        public const string Test = "AiPrompts.Test";
        public const string Export = "AiPrompts.Export";
        public const string Import = "AiPrompts.Import";
    }

    public static class AiInvocations
    {
        public const string View = "AiInvocations.View";
    }

    public static class AiDashboard
    {
        public const string View = "AiDashboard.View";
    }

    /// <summary>The Control Panel operator assistant (the floating help chat).
    /// Any CP team may use it; the answers are already filtered to the pages the
    /// signed-in user can see, so it never points anyone at a page they lack.</summary>
    public static class Assistant
    {
        public const string Use = "Assistant.Use";
    }

    /// <summary>D-735 — the transactional email-template editor.</summary>
    public static class EmailTemplates
    {
        public const string View = "EmailTemplates.View";
        public const string Edit = "EmailTemplates.Edit";
    }

    // ── Content ──────────────────────────────────────────────────────────

    /// <summary>Dynamic CMS content blocks (upsert covers create + edit).</summary>
    public static class ContentBlocks
    {
        public const string View = "ContentBlocks.View";
        public const string Edit = "ContentBlocks.Edit";
        public const string Delete = "ContentBlocks.Delete";
        public const string Export = "ContentBlocks.Export";
        public const string Import = "ContentBlocks.Import";
    }

    public static class Banners
    {
        public const string View = "Banners.View";
        public const string Create = "Banners.Create";
        public const string Edit = "Banners.Edit";
        public const string Delete = "Banners.Delete";
        public const string Export = "Banners.Export";
        public const string Import = "Banners.Import";
    }

    public static class Media
    {
        public const string View = "Media.View";
        public const string Create = "Media.Create";
        public const string Edit = "Media.Edit";
        public const string Delete = "Media.Delete";
        public const string Export = "Media.Export";
        public const string Import = "Media.Import";
    }

    /// <summary>D-568 — the centralized file store. <see cref="Upload"/> /
    /// <see cref="Delete"/> gate the single file API; <see cref="View"/> gates the
    /// management grid. The privileged trio (<see cref="ForceDelete"/> /
    /// <see cref="Reclassify"/> / <see cref="BulkExport"/>) is held separately so
    /// each elevated action is independently grantable and audited. (Public-file
    /// reads need no permission; private downloads reuse the owning entity's
    /// view permission, enforced per-service by the download endpoint.)</summary>
    public static class Files
    {
        public const string Upload = "Files.Upload";
        public const string Delete = "Files.Delete";
        public const string View = "Files.View";
        public const string ForceDelete = "Files.ForceDelete";
        public const string Reclassify = "Files.Reclassify";
        public const string BulkExport = "Files.BulkExport";
    }

    public static class News
    {
        public const string View = "News.View";
        public const string Create = "News.Create";
        public const string Edit = "News.Edit";
        public const string Delete = "News.Delete";
        public const string Export = "News.Export";
        public const string Import = "News.Import";
    }

    /// <summary>P2.1 (D-211) — FAQ management (two-level group → entry).</summary>
    public static class Faq
    {
        public const string View = "Faq.View";
        public const string Create = "Faq.Create";
        public const string Edit = "Faq.Edit";
        public const string Delete = "Faq.Delete";
    }

    /// <summary>Contact-us inbox (Figma 1388:7567) — triage the inquiries the
    /// app's "تواصل معنا" form submits.</summary>
    public static class ContactInquiries
    {
        public const string View = "ContactInquiries.View";
        public const string Manage = "ContactInquiries.Manage";
    }

    public static class MediaPartners
    {
        public const string View = "MediaPartners.View";
        public const string Create = "MediaPartners.Create";
        public const string Edit = "MediaPartners.Edit";
        public const string Delete = "MediaPartners.Delete";
        public const string Export = "MediaPartners.Export";
        public const string Import = "MediaPartners.Import";
    }

    public static class Archive
    {
        public const string View = "Archive.View";
        public const string Create = "Archive.Create";
        public const string Edit = "Archive.Edit";
        public const string Delete = "Archive.Delete";
        public const string Snapshot = "Archive.Snapshot";
        public const string Export = "Archive.Export";
        public const string Import = "Archive.Import";
    }

    // ── System & operations ──────────────────────────────────────────────

    public static class Statistics
    {
        public const string View = "Statistics.View";
    }

    /// <summary>FR-506 (SRS §3.5; FDS-003 §5.5) — read-only session-attendance
    /// dashboard over the HallAttendance arrival records (D-241).</summary>
    public static class Attendance
    {
        public const string View = "Attendance.View";
    }

    /// <summary>Gate Module (D-148). <see cref="Manage"/>, <see cref="Operate"/>
    /// and <see cref="ViewOwnReports"/> pre-date this catalogue and keep their
    /// exact strings.</summary>
    public static class Gates
    {
        public const string Manage = "Gates.Manage";
        public const string Operate = "Gates.Operate";
        public const string ViewOwnReports = "Gates.ViewOwnReports";
        public const string Export = "Gates.Export";
        public const string Import = "Gates.Import";
    }

    /// <summary>Operations toggles (registration / archive visibility).</summary>
    public static class Operations
    {
        public const string View = "Operations.View";
        public const string Edit = "Operations.Edit";
    }

    /// <summary>P2.4 (D-229) — System Configuration page (FDS-012 §5.5).</summary>
    public static class Configuration
    {
        public const string View = "Configuration.View";
        public const string Create = "Configuration.Create";
        public const string Edit = "Configuration.Edit";
        public const string Delete = "Configuration.Delete";
        public const string Export = "Configuration.Export";
        public const string Import = "Configuration.Import";
    }

    /// <summary>D-495 — the Organization / About profile editor (single-record
    /// edition-generic branding config). View opens it; Manage saves changes.</summary>
    public static class OrganizationProfile
    {
        public const string View = "OrganizationProfile.View";
        public const string Manage = "OrganizationProfile.Manage";
    }

    public static class OperationLog
    {
        public const string View = "OperationLog.View";

        // P1.6 — XLSX export of the (filtered) operation log.
        public const string Export = "OperationLog.Export";
    }

    public static class Logs
    {
        public const string View = "Logs.View";
    }

    /// <summary>The background-services monitor: live health of the in-process
    /// hosted workers (reminders, sweeps, the email queue drain).</summary>
    public static class ServicesMonitor
    {
        public const string View = "ServicesMonitor.View";
    }

    /// <summary>Invitations (D-168). <see cref="Manage"/> pre-dates this
    /// catalogue and keeps its exact string.</summary>
    public static class Invitations
    {
        public const string View = "Invitations.View";
        public const string Manage = "Invitations.Manage";
        public const string Export = "Invitations.Export";
    }

    /// <summary>VIP list + bulk notify (D-168). Both codes pre-date this
    /// catalogue and keep their exact strings.</summary>
    public static class Vips
    {
        public const string View = "Vips.View";
        public const string Notify = "Vips.Notify";
        public const string Export = "Vips.Export";
    }

    /// <summary>Control Panel "Announcements" desk — compose + send a manual
    /// notification broadcast (in-app + email) to a specific session's attendees or
    /// a broad audience, and view the send history.</summary>
    public static class Announcements
    {
        public const string Send = "Announcements.Send";
        public const string View = "Announcements.View";
    }

    // These baseline-role lists MUST be declared before `All`: static field
    // initializers run in textual order, and `BuildAll()` reads them — declaring
    // them after `All` would capture their null defaults (seeding every entry
    // with null BaselineRoles, which then NREs the seeder's grant loop).
    private static readonly IReadOnlyList<string> AdminOnly = [];
    private static readonly IReadOnlyList<string> GateOperator = [AppRoles.GateOperator];
    private static readonly IReadOnlyList<string> PublicRelations = [AppRoles.PublicRelations];

    // D-752 — the Security + Scientific team CP permission sets.
    private static readonly IReadOnlyList<string> SecurityTeam = [AppRoles.SecurityTeam];
    private static readonly IReadOnlyList<string> ScientificCommittee = [AppRoles.ScientificCommittee];

    // The two gate codes the GateOperator already holds are also granted to the
    // Security team, so their baseline list carries both roles.
    private static readonly IReadOnlyList<string> GateOperatorOrSecurity =
        [AppRoles.GateOperator, AppRoles.SecurityTeam];

    // Every built-in non-Administrator CP team, for permissions that any operator
    // should hold (e.g. the help assistant). Derived from the single source of
    // truth (AppRoles.CpRoles) so a future role added there is granted
    // automatically; Administrator is excluded because it gets everything via the
    // "*" wildcard (BaselineRoles list only the non-Administrator roles). Must be
    // declared before `All` (static-init order).
    private static readonly IReadOnlyList<string> AllCpTeams =
        AppRoles.CpRoles.Where(role => role != AppRoles.Administrator).ToList();

    /// <summary>Every permission in the catalogue, in display order. The
    /// seeder inserts one row per entry (idempotent by <see cref="PermissionDef.Code"/>).</summary>
    public static readonly IReadOnlyList<PermissionDef> All = BuildAll();

    private static List<PermissionDef> BuildAll() =>
    [
        // People & accounts
        new(Admins.View, "Admins", "View", "View admin accounts", AdminOnly),
        new(Admins.Create, "Admins", "Create", "Create admin accounts", AdminOnly),
        new(Admins.Delete, "Admins", "Delete", "Delete admin accounts", AdminOnly),
        new(Admins.Approve, "Admins", "Approve", "Approve pending admins", AdminOnly),
        new(Admins.Reject, "Admins", "Reject", "Reject pending admins", AdminOnly),
        new(Admins.Export, "Admins", "Export", "Export admins", AdminOnly),
        new(Admins.Import, "Admins", "Import", "Import admins", AdminOnly),
        new(Admins.ResetTwoFactor, "Admins", "ResetTwoFactor", "Reset a user's two-factor", AdminOnly),
        new(Admins.AssignRoles, "Admins", "AssignRoles", "Assign roles to admins", AdminOnly),

        new(Others.View, "Others", "View", "View other accounts", AdminOnly),
        new(Others.Create, "Others", "Create", "Create other accounts", AdminOnly),
        new(Others.Edit, "Others", "Edit", "Edit other accounts", AdminOnly),
        new(Others.Delete, "Others", "Delete", "Delete other accounts", AdminOnly),
        new(Others.Approve, "Others", "Approve", "Approve pending others", AdminOnly),
        new(Others.Reject, "Others", "Reject", "Reject pending others", AdminOnly),
        new(Others.Export, "Others", "Export", "Export other accounts", AdminOnly),
        new(Others.Import, "Others", "Import", "Import other accounts", AdminOnly),
        new(Others.RegisterOnsite, "Others", "RegisterOnsite", "Walk-in register an other account", AdminOnly),

        new(Visitors.View, "Visitors", "View", "View visitor accounts", AdminOnly),
        new(Visitors.Create, "Visitors", "Create", "Create visitor accounts", AdminOnly),
        new(Visitors.Edit, "Visitors", "Edit", "Edit visitor accounts", AdminOnly),
        new(Visitors.Delete, "Visitors", "Delete", "Delete visitor accounts", AdminOnly),
        new(Visitors.Approve, "Visitors", "Approve", "Approve pending visitors", AdminOnly),
        new(Visitors.Reject, "Visitors", "Reject", "Reject pending visitors", AdminOnly),
        new(Visitors.Export, "Visitors", "Export", "Export visitors", AdminOnly),
        new(Visitors.Import, "Visitors", "Import", "Import visitors", AdminOnly),
        new(Visitors.RegisterOnsite, "Visitors", "RegisterOnsite", "Walk-in register a visitor", AdminOnly),
        new(Visitors.BulkGenerate, "Visitors", "BulkGenerate", "Bulk-generate placeholder badges (visitors / delegates)", AdminOnly),
        new(Visitors.ViewBatches, "Visitors", "ViewBatches", "View bulk-badge batches", AdminOnly),
        new(Visitors.ManageBatches, "Visitors", "ManageBatches", "Re-email / revoke bulk-badge batches", AdminOnly),
        new(Visitors.ExportVip, "Visitors", "ExportVip", "Export the VVIP/VIP welcome roster (Mawj)", AdminOnly),

        new(Accounts.ChangeType, "Accounts", "ChangeType", "Change an account's type (Visitor <-> Other)", AdminOnly),

        new(Attendees.View, "Attendees", "View", "View the attendee roster", AdminOnly),
        new(Attendees.PrintBag, "Attendees", "PrintBag", "Print attendee badges", AdminOnly),
        new(Attendees.Export, "Attendees", "Export", "Export the attendee roster", AdminOnly),

        new(Roles.View, "Roles", "View", "View roles", AdminOnly),
        new(Roles.Create, "Roles", "Create", "Create roles", AdminOnly),
        new(Roles.Edit, "Roles", "Edit", "Rename roles", AdminOnly),
        new(Roles.Delete, "Roles", "Delete", "Delete roles", AdminOnly),
        new(Roles.AssignPermissions, "Roles", "AssignPermissions", "Assign permissions to roles", AdminOnly),
        new(Roles.Export, "Roles", "Export", "Export roles", AdminOnly),
        new(Roles.Import, "Roles", "Import", "Import roles", AdminOnly),

        new(Interests.View, "Interests", "View", "View interests", AdminOnly),
        new(Interests.Create, "Interests", "Create", "Create interests", AdminOnly),
        new(Interests.Edit, "Interests", "Edit", "Edit interests", AdminOnly),
        new(Interests.Delete, "Interests", "Delete", "Delete interests", AdminOnly),
        new(Interests.Export, "Interests", "Export", "Export interests", AdminOnly),
        new(Interests.Import, "Interests", "Import", "Import interests", AdminOnly),

        new(Countries.View, "Countries", "View", "View countries", AdminOnly),
        new(Countries.Create, "Countries", "Create", "Create countries", AdminOnly),
        new(Countries.Edit, "Countries", "Edit", "Edit countries", AdminOnly),
        new(Countries.Delete, "Countries", "Delete", "Delete countries", AdminOnly),
        new(Countries.Export, "Countries", "Export", "Export countries", AdminOnly),
        new(Countries.Import, "Countries", "Import", "Import countries", AdminOnly),

        new(ProfileTypes.View, "ProfileTypes", "View", "View profile types", AdminOnly),
        new(ProfileTypes.Create, "ProfileTypes", "Create", "Create profile types", AdminOnly),
        new(ProfileTypes.Edit, "ProfileTypes", "Edit", "Edit profile types", AdminOnly),
        new(ProfileTypes.Delete, "ProfileTypes", "Delete", "Delete profile types", AdminOnly),

        new(Organisations.View, "Organisations", "View", "View organisations", AdminOnly),
        new(Organisations.Create, "Organisations", "Create", "Create organisations", AdminOnly),
        new(Organisations.Edit, "Organisations", "Edit", "Edit organisations", AdminOnly),
        new(Organisations.Delete, "Organisations", "Delete", "Delete organisations", AdminOnly),
        new(Organisations.Import, "Organisations", "Import", "Import organisations from Excel", AdminOnly),
        new(Organisations.Export, "Organisations", "Export", "Export organisations", AdminOnly),

        // Administrative-regions lookup (the app region picker reads this table).
        new(Regions.View, "Regions", "View", "View regions", AdminOnly),
        new(Regions.Create, "Regions", "Create", "Create regions", AdminOnly),
        new(Regions.Edit, "Regions", "Edit", "Edit regions", AdminOnly),
        new(Regions.Delete, "Regions", "Delete", "Delete regions", AdminOnly),

        // Programme
        new(Themes.View, "Themes", "View", "View themes", AdminOnly),
        new(Themes.Create, "Themes", "Create", "Create themes", AdminOnly),
        new(Themes.Edit, "Themes", "Edit", "Edit themes", AdminOnly),
        new(Themes.Delete, "Themes", "Delete", "Delete themes", AdminOnly),
        new(Themes.Export, "Themes", "Export", "Export themes", AdminOnly),
        new(Themes.Import, "Themes", "Import", "Import themes", AdminOnly),

        // B9b — D-226: session categories (dynamic lookup).
        new(SessionCategories.View, "SessionCategories", "View", "View session categories", AdminOnly),
        new(SessionCategories.Create, "SessionCategories", "Create", "Create session categories", AdminOnly),
        new(SessionCategories.Edit, "SessionCategories", "Edit", "Edit session categories", AdminOnly),
        new(SessionCategories.Delete, "SessionCategories", "Delete", "Delete session categories", AdminOnly),
        new(SessionCategories.Export, "SessionCategories", "Export", "Export session categories", AdminOnly),
        new(SessionCategories.Import, "SessionCategories", "Import", "Import session categories", AdminOnly),

        // D-752 — the Scientific team runs the session programme.
        new(Sessions.View, "Sessions", "View", "View sessions", ScientificCommittee),
        new(Sessions.Create, "Sessions", "Create", "Create sessions", ScientificCommittee),
        new(Sessions.Edit, "Sessions", "Edit", "Edit sessions", ScientificCommittee),
        new(Sessions.Delete, "Sessions", "Delete", "Delete sessions", ScientificCommittee),
        // P3.2 — D-231: session broadcast lifecycle (mark held/recorded, publish).
        new(Sessions.Publish, "Sessions", "Publish", "Publish sessions & manage their lifecycle", ScientificCommittee),
        new(Sessions.Export, "Sessions", "Export", "Export sessions", ScientificCommittee),
        new(Sessions.Import, "Sessions", "Import", "Import sessions", ScientificCommittee),

        // D-452 — programme days (date + bilingual title + logo).
        // D-752 — the Scientific team maintains the programme-day banners.
        new(ProgrammeDays.View, "ProgrammeDays", "View", "View programme days", ScientificCommittee),
        new(ProgrammeDays.Create, "ProgrammeDays", "Create", "Create programme days", ScientificCommittee),
        new(ProgrammeDays.Edit, "ProgrammeDays", "Edit", "Edit programme days", ScientificCommittee),
        new(ProgrammeDays.Delete, "ProgrammeDays", "Delete", "Delete programme days", ScientificCommittee),
        new(ProgrammeDays.Export, "ProgrammeDays", "Export", "Export programme days", ScientificCommittee),
        new(ProgrammeDays.Import, "ProgrammeDays", "Import", "Import programme days", ScientificCommittee),

        new(ProgrammeTimeline.View, "ProgrammeTimeline", "View", "View the programme timeline", AdminOnly),

        new(Halls.View, "Halls", "View", "View halls", AdminOnly),
        new(Halls.Create, "Halls", "Create", "Create halls", AdminOnly),
        new(Halls.Edit, "Halls", "Edit", "Edit halls", AdminOnly),
        new(Halls.Delete, "Halls", "Delete", "Delete halls", AdminOnly),
        new(Halls.Export, "Halls", "Export", "Export halls", AdminOnly),
        new(Halls.Import, "Halls", "Import", "Import halls", AdminOnly),

        new(SeatLayouts.View, "SeatLayouts", "View", "View hall seat layouts", AdminOnly),
        new(SeatLayouts.Edit, "SeatLayouts", "Edit", "Edit hall seat layouts", AdminOnly),
        new(SeatLayouts.Delete, "SeatLayouts", "Delete", "Delete hall seat layouts", AdminOnly),

        new(SeatPlans.View, "SeatPlans", "View", "View session seat plans", AdminOnly),
        new(SeatPlans.Edit, "SeatPlans", "Edit", "Edit session seat plans", AdminOnly),

        // D-771 — the staff seating desk (app-side operational capability; also
        // grantable to a Control-Panel role so a desk supervisor can hold it).
        new(Seating.Assist, "Seating", "Assist", "Assist guests with seating (staff seating desk)", AdminOnly),

        // #6/#17 — read-only booking monitor (approve/reject retired: no approval step).
        new(Bookings.View, "Bookings", "View", "View the booking monitor", AdminOnly),
        new(Bookings.Export, "Bookings", "Export", "Export bookings to Excel", AdminOnly),

        // SIMF-FDS-013 — D-248: flexible hall config + B2B/B2C business meetings.
        new(MeetingTables.View, "MeetingTables", "View", "View meeting tables", AdminOnly),
        new(MeetingTables.Edit, "MeetingTables", "Edit", "Define / generate meeting tables", AdminOnly),
        new(MeetingTables.Export, "MeetingTables", "Export", "Export meeting tables", AdminOnly),
        new(HallAllocations.View, "HallAllocations", "View", "View hall allocations", AdminOnly),
        new(HallAllocations.Edit, "HallAllocations", "Edit", "Reserve / release hall allocations", AdminOnly),
        // QA A36 — hall meeting-time windows: hall-scoped, read by both meeting desks.
        new(HallAvailability.View, "HallAvailability", "View", "View hall availability windows & free slots", AdminOnly),
        new(HallAvailability.Manage, "HallAvailability", "Manage", "Define / delete hall availability windows", AdminOnly),
        new(BusinessMeetings.View, "BusinessMeetings", "View", "View business meetings", AdminOnly),
        new(BusinessMeetings.Schedule, "BusinessMeetings", "Schedule", "Schedule business meetings", AdminOnly),
        new(BusinessMeetings.Cancel, "BusinessMeetings", "Cancel", "Cancel business meetings", AdminOnly),
        new(BusinessMeetings.Export, "BusinessMeetings", "Export", "Export business meetings", AdminOnly),

        new(SpeakerMeetingRequests.View, "SpeakerMeetingRequests", "View", "View speaker meeting requests", AdminOnly),
        new(SpeakerMeetingRequests.Manage, "SpeakerMeetingRequests", "Manage", "Manage speaker meeting requests", AdminOnly),
        new(SpeakerMeetingRequests.Export, "SpeakerMeetingRequests", "Export", "Export speaker meeting requests", AdminOnly),

        new(DelegationMeetings.View, "DelegationMeetings", "View", "View delegation meeting requests", AdminOnly),
        new(DelegationMeetings.Manage, "DelegationMeetings", "Manage", "Manage delegation meeting requests", AdminOnly),

        new(ParticipationDocumentRequests.View, "ParticipationDocumentRequests", "View", "View participation document requests", AdminOnly),
        new(ParticipationDocumentRequests.Manage, "ParticipationDocumentRequests", "Manage", "Manage participation document requests", AdminOnly),

        new(BadgeUpdateRequests.View, "BadgeUpdateRequests", "View", "View badge update requests", AdminOnly),
        new(BadgeUpdateRequests.Manage, "BadgeUpdateRequests", "Manage", "Manage badge update requests", AdminOnly),

        // D-752 — the Scientific team curates the speaker roster.
        new(Speakers.View, "Speakers", "View", "View speakers", ScientificCommittee),
        new(Speakers.Create, "Speakers", "Create", "Create speakers", ScientificCommittee),
        new(Speakers.Edit, "Speakers", "Edit", "Edit speakers", ScientificCommittee),
        new(Speakers.Delete, "Speakers", "Delete", "Delete speakers", ScientificCommittee),
        new(Speakers.Export, "Speakers", "Export", "Export speakers", ScientificCommittee),
        new(Speakers.Import, "Speakers", "Import", "Import speakers", ScientificCommittee),

        new(SessionModerators.View, "SessionModerators", "View", "View session moderators", AdminOnly),
        new(SessionModerators.Assign, "SessionModerators", "Assign", "Assign session moderators", AdminOnly),
        new(SessionModerators.Revoke, "SessionModerators", "Revoke", "Revoke session moderators", AdminOnly),
        new(SessionModerators.Export, "SessionModerators", "Export", "Export session moderators", AdminOnly),

        // D-752 — the Scientific team runs the live-session moderation desk.
        new(SessionModeration.Moderate, "SessionModeration", "Moderate", "Moderate a live session", ScientificCommittee),

        // P3.3 — D-212: Scientific-Committee central Q&A queue.
        // D-752 — the Scientific team now holds this queue as a seeded baseline.
        new(Questions.View, "Questions", "View", "View the question queue", ScientificCommittee),
        new(Questions.Moderate, "Questions", "Moderate", "Approve / hide questions", ScientificCommittee),
        new(Questions.Escalate, "Questions", "Escalate", "Escalate questions to a role", ScientificCommittee),
        new(Questions.Export, "Questions", "Export", "Export the question queue", ScientificCommittee),

        // P4.1 — D-238: AI session-summary / محضر committee desk.
        // D-752 — the Scientific team owns the محضر / summary desk.
        new(SessionSummaries.View, "SessionSummaries", "View", "View session summaries", ScientificCommittee),
        new(SessionSummaries.Edit, "SessionSummaries", "Edit", "Generate / edit session summaries", ScientificCommittee),
        new(SessionSummaries.Publish, "SessionSummaries", "Publish", "Publish / un-publish session summaries", ScientificCommittee),
        new(SessionSummaries.Approve, "SessionSummaries", "Approve", "Approve session summaries (ready for the host/moderator)", ScientificCommittee),
        new(SessionSummaries.Export, "SessionSummaries", "Export", "Export session summaries", ScientificCommittee),

        // P5.1d — D-244: hall-door arrival console (operator QR scan).
        // D-752 — part of the Security team's access-control surface.
        new(HallArrivals.View, "HallArrivals", "View", "View the hall-arrival console", SecurityTeam),
        // FR-CHK-003 — the one code gates BOTH door actions (the arrivals AND the
        // departures endpoint, and both console buttons); the old wording named only
        // the arrival, so an admin granting it could not tell it also allows check-out.
        new(HallArrivals.Record, "HallArrivals", "Record", "Record a hall arrival or departure by badge scan", SecurityTeam),

        // Exhibition
        new(Exhibitors.View, "Exhibitors", "View", "View exhibitors", AdminOnly),
        new(Exhibitors.Create, "Exhibitors", "Create", "Create exhibitors", AdminOnly),
        new(Exhibitors.Edit, "Exhibitors", "Edit", "Edit exhibitors", AdminOnly),
        new(Exhibitors.Delete, "Exhibitors", "Delete", "Delete exhibitors", AdminOnly),
        new(Exhibitors.Export, "Exhibitors", "Export", "Export exhibitors", AdminOnly),
        new(Exhibitors.Import, "Exhibitors", "Import", "Import exhibitors", AdminOnly),

        new(Booths.View, "Booths", "View", "View booths", AdminOnly),
        new(Booths.Create, "Booths", "Create", "Create booths", AdminOnly),
        new(Booths.Edit, "Booths", "Edit", "Edit booths", AdminOnly),
        new(Booths.Delete, "Booths", "Delete", "Delete booths", AdminOnly),
        new(Booths.Export, "Booths", "Export", "Export booths", AdminOnly),
        new(Booths.Import, "Booths", "Import", "Import booths", AdminOnly),

        new(Sponsors.View, "Sponsors", "View", "View sponsors", AdminOnly),
        new(Sponsors.Create, "Sponsors", "Create", "Create sponsors", AdminOnly),
        new(Sponsors.Edit, "Sponsors", "Edit", "Edit sponsors", AdminOnly),
        new(Sponsors.Delete, "Sponsors", "Delete", "Delete sponsors", AdminOnly),
        new(Sponsors.Export, "Sponsors", "Export", "Export sponsors", AdminOnly),
        new(Sponsors.Import, "Sponsors", "Import", "Import sponsors", AdminOnly),

        // P2.5 — D-230: 2D venue map editor.
        new(VenueMap.View, "VenueMap", "View", "View the venue map", AdminOnly),
        new(VenueMap.Create, "VenueMap", "Create", "Create venue-map nodes", AdminOnly),
        new(VenueMap.Edit, "VenueMap", "Edit", "Edit venue-map nodes", AdminOnly),
        new(VenueMap.Delete, "VenueMap", "Delete", "Delete venue-map nodes", AdminOnly),
        new(VenueMap.Export, "VenueMap", "Export", "Export venue-map nodes", AdminOnly),
        new(VenueMap.Import, "VenueMap", "Import", "Import venue-map nodes", AdminOnly),

        // Engagement
        // D-752 — the Scientific team reviews session ratings + feedback.
        new(Ratings.View, "Ratings", "View", "View ratings and feedback", ScientificCommittee),
        new(Ratings.Export, "Ratings", "Export", "Export ratings and feedback", ScientificCommittee),
        new(RatingConfig.View, "RatingConfig", "View", "View rating configuration", AdminOnly),
        new(RatingConfig.Create, "RatingConfig", "Create", "Create rating types/questions", AdminOnly),
        new(RatingConfig.Edit, "RatingConfig", "Edit", "Edit rating types/questions", AdminOnly),
        new(RatingConfig.Delete, "RatingConfig", "Delete", "Delete rating types/questions", AdminOnly),

        // Knowledge
        new(AiPrompts.View, "AiPrompts", "View", "View AI prompts", AdminOnly),
        new(AiPrompts.Create, "AiPrompts", "Create", "Create AI prompts", AdminOnly),
        new(AiPrompts.Edit, "AiPrompts", "Edit", "Edit AI prompts", AdminOnly),
        new(AiPrompts.Delete, "AiPrompts", "Delete", "Delete AI prompts", AdminOnly),
        new(AiPrompts.Test, "AiPrompts", "Test", "Test AI prompts", AdminOnly),
        new(AiPrompts.Export, "AiPrompts", "Export", "Export AI prompts", AdminOnly),
        new(AiPrompts.Import, "AiPrompts", "Import", "Import AI prompts", AdminOnly),

        new(AiInvocations.View, "AiInvocations", "View", "View AI invocations log", AdminOnly),
        new(AiDashboard.View, "AiDashboard", "View", "View the AI dashboard", AdminOnly),
        new(Assistant.Use, "Assistant", "Use", "Use the Control Panel help assistant", AllCpTeams),

        new(EmailTemplates.View, "EmailTemplates", "View", "View email templates", AdminOnly),
        new(EmailTemplates.Edit, "EmailTemplates", "Edit", "Edit email templates", AdminOnly),

        // Content
        new(ContentBlocks.View, "ContentBlocks", "View", "View content blocks", AdminOnly),
        new(ContentBlocks.Edit, "ContentBlocks", "Edit", "Edit content blocks", AdminOnly),
        new(ContentBlocks.Delete, "ContentBlocks", "Delete", "Delete content blocks", AdminOnly),
        new(ContentBlocks.Export, "ContentBlocks", "Export", "Export content blocks", AdminOnly),
        new(ContentBlocks.Import, "ContentBlocks", "Import", "Import content blocks", AdminOnly),

        new(Banners.View, "Banners", "View", "View banners", AdminOnly),
        new(Banners.Create, "Banners", "Create", "Create banners", AdminOnly),
        new(Banners.Edit, "Banners", "Edit", "Edit banners", AdminOnly),
        new(Banners.Delete, "Banners", "Delete", "Delete banners", AdminOnly),
        new(Banners.Export, "Banners", "Export", "Export banners", AdminOnly),
        new(Banners.Import, "Banners", "Import", "Import banners", AdminOnly),

        new(Media.View, "Media", "View", "View media gallery", AdminOnly),
        new(Media.Create, "Media", "Create", "Create media items", AdminOnly),
        new(Media.Edit, "Media", "Edit", "Edit media items", AdminOnly),
        new(Media.Delete, "Media", "Delete", "Delete media items", AdminOnly),
        new(Media.Export, "Media", "Export", "Export media items", AdminOnly),
        new(Media.Import, "Media", "Import", "Import media items", AdminOnly),

        // News is PR/marketing territory: the admin News endpoints were gated
        // by PublicRelationsAccess before this catalogue, so the PublicRelations
        // role keeps News as a seeded baseline grant (preserves prior behaviour).
        new(News.View, "News", "View", "View news articles", PublicRelations),
        new(News.Create, "News", "Create", "Create news articles", PublicRelations),
        new(News.Edit, "News", "Edit", "Edit news articles", PublicRelations),
        new(News.Delete, "News", "Delete", "Delete news articles", PublicRelations),
        new(News.Export, "News", "Export", "Export news articles", PublicRelations),
        new(News.Import, "News", "Import", "Import news articles", PublicRelations),

        new(Faq.View, "Faq", "View", "View FAQ groups + entries", AdminOnly),
        new(Faq.Create, "Faq", "Create", "Create FAQ groups + entries", AdminOnly),
        new(Faq.Edit, "Faq", "Edit", "Edit FAQ groups + entries", AdminOnly),
        new(Faq.Delete, "Faq", "Delete", "Delete FAQ groups + entries", AdminOnly),

        new(ContactInquiries.View, "ContactInquiries", "View", "View contact-us inquiries", AdminOnly),
        new(ContactInquiries.Manage, "ContactInquiries", "Manage", "Mark contact-us inquiries handled", AdminOnly),

        new(MediaPartners.View, "MediaPartners", "View", "View media partners", AdminOnly),
        new(MediaPartners.Create, "MediaPartners", "Create", "Create media partners", AdminOnly),
        new(MediaPartners.Edit, "MediaPartners", "Edit", "Edit media partners", AdminOnly),
        new(MediaPartners.Delete, "MediaPartners", "Delete", "Delete media partners", AdminOnly),
        new(MediaPartners.Export, "MediaPartners", "Export", "Export media partners", AdminOnly),
        new(MediaPartners.Import, "MediaPartners", "Import", "Import media partners", AdminOnly),

        new(Archive.View, "Archive", "View", "View archive editions", AdminOnly),
        new(Archive.Create, "Archive", "Create", "Create archive editions", AdminOnly),
        new(Archive.Edit, "Archive", "Edit", "Edit archive editions", AdminOnly),
        new(Archive.Delete, "Archive", "Delete", "Delete archive editions", AdminOnly),

        // D-357 — centralised media library (manage every unified media asset).
        new(MediaLibrary.View, "MediaLibrary", "View", "View the media library", AdminOnly),
        new(MediaLibrary.Manage, "MediaLibrary", "Manage", "Manage media assets (deactivate / restore / edit links)", AdminOnly),

        // D-568 — centralized file store.
        new(Files.Upload, "Files", "Upload", "Upload a file to the central store", AdminOnly),
        new(Files.Delete, "Files", "Delete", "Delete a file from the central store", AdminOnly),
        new(Files.View, "Files", "View", "View the central file management grid", AdminOnly),
        new(Files.ForceDelete, "Files", "ForceDelete", "Force-delete a retained file (privileged)", AdminOnly),
        new(Files.Reclassify, "Files", "Reclassify", "Change a file's service / classification (privileged)", AdminOnly),
        new(Files.BulkExport, "Files", "BulkExport", "Bulk-export files (privileged)", AdminOnly),
        new(Archive.Snapshot, "Archive", "Snapshot", "Snapshot the current event into a past edition", AdminOnly),
        new(Archive.Export, "Archive", "Export", "Export archive editions", AdminOnly),
        new(Archive.Import, "Archive", "Import", "Import archive editions", AdminOnly),

        // System & operations
        new(Statistics.View, "Statistics", "View", "View the statistics dashboard", AdminOnly),
        // D-752 — the Security team monitors session attendance.
        new(Attendance.View, "Attendance", "View", "View the session-attendance dashboard", SecurityTeam),

        // D-752 — the Security team owns the gate access-control surface (and
        // shares Operate / ViewOwnReports with the existing GateOperator).
        new(Gates.Manage, "Gates", "Manage", "Manage gates", SecurityTeam),
        new(Gates.Operate, "Gates", "Operate", "Operate a gate", GateOperatorOrSecurity),
        new(Gates.ViewOwnReports, "Gates", "ViewOwnReports", "View own gate reports", GateOperatorOrSecurity),
        new(Gates.Export, "Gates", "Export", "Export gates", SecurityTeam),
        new(Gates.Import, "Gates", "Import", "Import gates", SecurityTeam),

        new(Operations.View, "Operations", "View", "View operations toggles", AdminOnly),
        new(Operations.Edit, "Operations", "Edit", "Change operations toggles", AdminOnly),

        // P2.4 — D-229: System Configuration settings.
        new(Configuration.View, "Configuration", "View", "View system configuration", AdminOnly),
        new(Configuration.Create, "Configuration", "Create", "Create system settings", AdminOnly),
        new(Configuration.Edit, "Configuration", "Edit", "Edit system settings", AdminOnly),
        new(Configuration.Delete, "Configuration", "Delete", "Delete system settings", AdminOnly),
        new(Configuration.Export, "Configuration", "Export", "Export system settings", AdminOnly),
        new(Configuration.Import, "Configuration", "Import", "Import system settings", AdminOnly),

        new(OrganizationProfile.View, "OrganizationProfile", "View", "View the organization profile", AdminOnly),
        new(OrganizationProfile.Manage, "OrganizationProfile", "Manage", "Edit the organization profile", AdminOnly),

        new(OperationLog.View, "OperationLog", "View", "View the operation log", AdminOnly),
        new(OperationLog.Export, "OperationLog", "Export", "Export the operation log", AdminOnly),
        new(Logs.View, "Logs", "View", "View system logs", AdminOnly),
        new(ServicesMonitor.View, "ServicesMonitor", "View", "View the background-services monitor", AdminOnly),

        new(Invitations.View, "Invitations", "View", "View invitations", PublicRelations),
        new(Invitations.Manage, "Invitations", "Manage", "Manage invitations", PublicRelations),
        new(Invitations.Export, "Invitations", "Export", "Export invitations", PublicRelations),

        new(Vips.View, "Vips", "View", "View the VIP list", PublicRelations),
        new(Vips.Notify, "Vips", "Notify", "Notify VIPs", PublicRelations),
        new(Vips.Export, "Vips", "Export", "Export the VIP list", PublicRelations),

        // Announcements desk — mass-notify is a high-privilege power, so it is
        // AdminOnly (Administrator via the "*" wildcard); grant to a comms role later.
        new(Announcements.Send, "Announcements", "Send", "Send notification broadcasts", AdminOnly),
        new(Announcements.View, "Announcements", "View", "View broadcast send history", AdminOnly),
    ];

    // ── App operational permissions by MobileAppRole (D-563) ──────────────
    // The capability a partner-side app user carries comes from their
    // ProfileType.MobileAppRole — NOT an admin RBAC role. ResolveMobileAppRole
    // only returns Staff/Moderator for an APPROVED partner account, so these
    // grants are gated by that admin-curated assignment + approval.

    /// <summary>Staff (gate operations + walk-in registration + look-ups).
    /// Scoped to exactly the staff app screens — `Others.RegisterOnsite` is
    /// deliberately NOT granted (no app screen registers partner "Other"
    /// accounts; that lives only on the CP desk), keeping least-privilege.</summary>
    private static readonly IReadOnlyList<string> StaffAppPermissions =
    [
        Gates.Operate,           // /app/gates scans + my-assignments
        Gates.ViewOwnReports,    // /app/gates visitor list + my reports
        Visitors.RegisterOnsite, // /app/staff/visitors/register-onsite
        Seating.Assist,          // D-771 — /app/staff/sessions/{id}/seating/*
    ];

    /// <summary>Moderator = Staff + content/user moderation.</summary>
    private static readonly IReadOnlyList<string> ModeratorAppPermissions =
    [
        Gates.Operate,
        Gates.ViewOwnReports,
        Visitors.RegisterOnsite,
        Seating.Assist,          // D-771 — the seating desk (Moderator = Staff + …)
        Questions.View,
        Questions.Moderate,
        SessionModeration.Moderate,
    ];

    /// <summary>
    /// D-563 — the operational permission codes an APP user receives purely from
    /// their resolved <see cref="MobileAppRole"/> (set on the user's ProfileType,
    /// never an admin RBAC role). This is how a partner-side Staff / Moderator
    /// attendee gains the on-ground capabilities their app screens need (gate
    /// scan, walk-in registration, moderation) without being a Control-Panel
    /// admin. Empty for None / Visitor / Exhibitor (the exhibitor lead-capture
    /// endpoints gate on the approved account only, no perm claim).
    /// </summary>
    public static IReadOnlyList<string> OperationalPermissionsForAppRole(
        MobileAppRole role) =>
        role switch
        {
            MobileAppRole.Staff => StaffAppPermissions,
            MobileAppRole.Moderator => ModeratorAppPermissions,
            _ => [],
        };
}
