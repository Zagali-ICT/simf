// Tests: SIMF.ControlPanel.Tests/CpNavigationTests.cs
using SIMF.Common;

namespace SIMF.ControlPanel;

/// <summary>
/// The Control Panel navigation map — the navigation groups and their modules
/// (SIMF-CPD-001 section 5.1). The former 23-item "System" group was split into
/// coherent groups (People absorbed the attendee accounts; new Scientific
/// committee / Public relations / Gates &amp; arrivals / Reference data groups).
/// Each label is a resource key resolved through
/// <see cref="Strings" />. Issue-1: each item carries a
/// <see cref="NavItem.RequiredPermission"/>; the shell hides items the
/// signed-in user lacks (Administrator's wildcard shows everything).
/// </summary>
public static class CpNavigation
{
    /// <summary>One navigation item — a module link. <c>IsStub</c> marks
    /// entries that currently resolve to <c>ModulePlaceholder</c>; the shell
    /// renders a "Soon" badge so operators know which menu items are real
    /// (D-132). <c>RequiredPermission</c> is the permission code that gates the
    /// item in the side menu (Issue-1); <c>null</c> means always visible to any
    /// signed-in user (the dashboard and the not-yet-built stubs). <c>Icon</c> is
    /// the leading <c>SimfIcon</c> key shown before the label in the side menu
    /// (sub-menu items only).</summary>
    public sealed record NavItem(
        string LabelKey, string Href, bool IsStub = false, string? RequiredPermission = null,
        string? Icon = null)
    {
        /// <summary>Whether an operator holding the given permission codes may
        /// reach this item: an ungated item is always visible, the Administrator
        /// wildcard sees everything, otherwise the operator must hold the item's
        /// permission. The single source of truth for nav visibility — shared by
        /// the side menu (<c>CpShellLayout</c>) and the help-assistant directory
        /// (<c>CpAssistantDirectory</c>).</summary>
        public bool IsPermittedFor(
            IReadOnlySet<string> permissions, bool hasAllPermissions) =>
            RequiredPermission is null
            || hasAllPermissions
            || permissions.Contains(RequiredPermission);
    }

    /// <summary>One navigation group — a heading and its items.</summary>
    public sealed record NavGroup(string LabelKey, IReadOnlyList<NavItem> Items);

    /// <summary>The navigation groups, in display order.</summary>
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("Nav.Overview",
        [
            new("Module.Dashboard", "/", Icon: "layout-dashboard"),
            // FR-506 — read-only session-attendance dashboard over HallAttendance.
            new("Module.Attendance", "/admin/attendance", RequiredPermission: PermissionCatalog.Attendance.View, Icon: "user-check"),
            // 2026-07-18 — live per-session hall view: 4-state seat grid + who's
            // currently inside the hall (over HallAttendance + the seat map).
            new("Module.SessionLiveHall", "/admin/sessions/live-hall", RequiredPermission: PermissionCatalog.Attendance.View, Icon: "monitor"),
            // §6.16 — /admin/statistics was an ORPHAN: a real page rendering live stat
            // tiles and gated on Statistics.View, but reachable from nowhere in the CP.
            // Granting that permission was silently inert; only a hand-typed URL opened it.
            // Icon is "bar-chart", NOT "chart-bar". SimfIcon throws
            // ArgumentException on an unknown name, and because this nav renders
            // on EVERY Control Panel page, the transposition introduced with this
            // row in 4ba30020 raised a render exception on 92 of 97 pages for a
            // day — invisible on screen, logged to the console. See
            // CpNavigationIconTests, which now pins every nav icon to the set
            // SimfIcon actually knows.
            new("Module.Statistics", "/admin/statistics", RequiredPermission: PermissionCatalog.Statistics.View, Icon: "bar-chart"),
        ]),
        new("Nav.People",
        [
            // Combined attendee roster over Visitors +
            // Others (read-only join on existing tables; no migration).
            new("Module.Attendees", "/admin/attendees", RequiredPermission: PermissionCatalog.Attendees.View, Icon: "users"),
            // P7e (D-055) — Visitor + Other attendee accounts and their pending
            // approval queues. Moved here from the old 23-item System group so the
            // people-management pages live together (the per-type Pending queues
            // deliver the registration approvals; Admins' own queue stays under
            // Access control).
            new("Module.AdminVisitors", "/admin/visitors", RequiredPermission: PermissionCatalog.Visitors.View, Icon: "user"),
            new("Module.AdminVisitorsPending", "/admin/visitors/pending", RequiredPermission: PermissionCatalog.Visitors.View, Icon: "hourglass"),
            // V-1 (D-429) — dedicated VVIP/VIP registration + the موج welcome-data export.
            new("Module.AdminVisitorsVip", "/admin/visitors/vip", RequiredPermission: PermissionCatalog.Visitors.RegisterOnsite, Icon: "star"),
            new("Module.AdminVisitorsVipExport", "/admin/visitors/vip/export", RequiredPermission: PermissionCatalog.Visitors.ExportVip, Icon: "download"),
            // Delegates (وفد): a separate desk for delegate registration + bulk badges.
            new("Module.AdminDelegates", "/admin/delegates", RequiredPermission: PermissionCatalog.Visitors.RegisterOnsite, Icon: "users"),
            // Persisted bulk-badge batches: re-email / revoke a generated set.
            new("Module.AdminBadgeBatches", "/admin/visitors/badge-batches", RequiredPermission: PermissionCatalog.Visitors.ViewBatches, Icon: "layers"),
            new("Module.AdminOthers", "/admin/others", RequiredPermission: PermissionCatalog.Others.View, Icon: "id-card"),
            new("Module.AdminOthersPending", "/admin/others/pending", RequiredPermission: PermissionCatalog.Others.View, Icon: "hourglass"),
            // Print-bag station: lookup by QR id + reprint badge.
            new("Module.PrintBag", "/admin/print-bag", RequiredPermission: PermissionCatalog.Attendees.PrintBag, Icon: "printer"),
        ]),
        // Issue-1 — access control grouped together: the admin accounts that
        // sign in to the CP, the roles & permissions they hold, and 2FA reset.
        new("Nav.AccessControl",
        [
            new("Module.AdminAdmins", "/admin/admins", RequiredPermission: PermissionCatalog.Admins.View, Icon: "shield"),
            new("Module.AdminAdminsPending", "/admin/admins/pending", RequiredPermission: PermissionCatalog.Admins.View, Icon: "hourglass"),
            // Roles + their per-page/per-action grants.
            new("Module.Roles", "/admin/roles", RequiredPermission: PermissionCatalog.Roles.View, Icon: "key"),
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa", RequiredPermission: PermissionCatalog.Admins.ResetTwoFactor, Icon: "rotate"),
        ]),
        new("Nav.Programme",
        [
            // Programme themes (SIMF-FDS-004 §5.1).
            new("Module.Themes", "/admin/themes", RequiredPermission: PermissionCatalog.Themes.View, Icon: "layers"),
            // Programme sessions (SIMF-FDS-004 §5.3 + PDF §2.9).
            new("Module.Sessions", "/admin/sessions", RequiredPermission: PermissionCatalog.Sessions.View, Icon: "calendar"),
            // B9b (D-226) — dynamic session-category lookup (SIMF-FDS-004 §5.4).
            new("Module.SessionCategories", "/admin/session-categories", RequiredPermission: PermissionCatalog.SessionCategories.View, Icon: "folder"),
            // Programme days (date + bilingual title + logo) for the app's Sessions screen.
            new("Module.ProgrammeDays", "/admin/programme-days", RequiredPermission: PermissionCatalog.ProgrammeDays.View, Icon: "calendar"),
            // Read-only run-of-show timeline over the existing sessions list.
            new("Module.ProgrammeTimeline", "/admin/programme/timeline", RequiredPermission: PermissionCatalog.ProgrammeTimeline.View, Icon: "clock"),
            // Venue halls (SIMF-FDS-004 §5.2).
            new("Module.Halls", "/admin/halls", RequiredPermission: PermissionCatalog.Halls.View, Icon: "building"),
            // D-182 (CP UI for D-175 seat reservations) — hall seat
            // layout editor + per-session seat plan.
            new("Module.HallSeatLayouts", "/admin/halls/seat-layouts", RequiredPermission: PermissionCatalog.SeatLayouts.View, Icon: "grid"),
            // Q6 (2026-07-30) asked for a CP surface for the per-hall arrival
            // boundary. There is deliberately NO separate geofence page:
            // HallsAddEdit.razor already edits GeofenceCenterLat /
            // GeofenceCenterLon / GeofenceRadiusMeters on the hall itself (gated
            // Halls.Create / Halls.Edit, validated in AdminHallService), so a
            // second page would be a duplicate editor for one field set. A nav
            // row pointing at an unbuilt /admin/halls/geofence was removed here.
            new("Module.SessionSeatPlans", "/admin/sessions/seat-plans", RequiredPermission: PermissionCatalog.SeatPlans.View, Icon: "armchair"),
            // Attendee meeting requests TO a speaker (Mockup page 20).
            new("Module.SpeakerMeetingRequests", "/admin/speaker-meeting-requests", RequiredPermission: PermissionCatalog.SpeakerMeetingRequests.View, Icon: "inbox"),
            // The team defines speaker availability windows for VIP meeting slots.
            new("Module.AdminSpeakerAvailability", "/admin/speaker-availability", RequiredPermission: PermissionCatalog.SpeakerMeetingRequests.Manage, Icon: "calendar"),
            // D-715 (item 7, FDS-013 §15 GAP-1) — the team defines a hall's meeting time (availability windows).
            // QA A36 — hall-scoped code (both meeting desks read the slots it produces).
            new("Module.AdminHallAvailability", "/admin/hall-availability", RequiredPermission: PermissionCatalog.HallAvailability.Manage, Icon: "calendar"),
            // Delegation↔delegation meeting requests review desk.
            new("Module.AdminDelegationMeetings", "/admin/delegation-meetings", RequiredPermission: PermissionCatalog.DelegationMeetings.View, Icon: "inbox"),
            // Bi-Meeting rework — the team defines a delegation/country's availability windows.
            new("Module.AdminDelegationAvailability", "/admin/delegation-availability", RequiredPermission: PermissionCatalog.DelegationMeetings.Manage, Icon: "calendar"),
            // D-500 (Wave 5, الطلبات) — participation-document + badge-update request review desks.
            new("Module.AdminDocumentRequests", "/admin/document-requests", RequiredPermission: PermissionCatalog.ParticipationDocumentRequests.View, Icon: "inbox"),
            new("Module.AdminBadgeRequests", "/admin/badge-requests", RequiredPermission: PermissionCatalog.BadgeUpdateRequests.View, Icon: "inbox"),
            // Programme speakers (SIMF-DAT-001 §5.4).
            new("Module.Speakers", "/admin/speakers", RequiredPermission: PermissionCatalog.Speakers.View, Icon: "mic"),
            // Speaker presentation files (FR-407). Reuses Speakers.*.
            new("Module.SpeakerPresentations", "/admin/speaker-presentations", RequiredPermission: PermissionCatalog.Speakers.View, Icon: "presentation"),
            // #6/#17 — read-only booking monitor.
            new("Module.Bookings", "/admin/bookings", RequiredPermission: PermissionCatalog.Bookings.View, Icon: "ticket"),
            // SIMF-FDS-013 (D-248) — flexible hall config: purpose + meeting tables
            // + hall allocations (whole / random-by-count / row-column).
            new("Module.MeetingTables", "/admin/meeting-tables", RequiredPermission: PermissionCatalog.MeetingTables.View, Icon: "table"),
            // SIMF-FDS-013 (D-248) — admin-arranged B2B/B2C business meetings.
            new("Module.BusinessMeetings", "/admin/business-meetings", RequiredPermission: PermissionCatalog.BusinessMeetings.View, Icon: "handshake"),
        ]),
        // Scientific committee desks (moved out of the old 23-item System group):
        // per-session moderator grants, the central Q&A queue, and the AI
        // session-summary / محضر desk — all session-bound, so they sit by Programme.
        new("Nav.ScientificCommittee",
        [
            // Per-session moderator grants admin CRUD
            // (PDF §2.7.2). The moderator's own live-queue desk is
            // /sessions/{id}/moderate — reached from the Sessions grid, not the nav.
            new("Module.SessionModerators", "/admin/session-moderators", RequiredPermission: PermissionCatalog.SessionModerators.View, Icon: "gavel"),
            // Scientific-Committee central Q&A queue (stage 2).
            new("Module.QuestionQueue", "/admin/question-queue", RequiredPermission: PermissionCatalog.Questions.View, Icon: "help-circle"),
            // Scientific-Committee AI session-summary / محضر desk.
            new("Module.SessionSummaries", "/admin/session-summaries", RequiredPermission: PermissionCatalog.SessionSummaries.View, Icon: "file-text"),
        ]),
        new("Nav.Exhibition",
        [
            // The "Exhibitors" stub was removed: exhibitor
            // onboarding is delivered by the real Exhibitors page (CP-only
            // exhibitor + account provisioning, D-202) plus Booths and Sponsors;
            // in-app exhibitor self-signup was permanently descoped.
            // Exhibitor CRUD + account provisioning.
            new("Module.Exhibitors", "/admin/exhibitors", RequiredPermission: PermissionCatalog.Exhibitors.View, Icon: "briefcase"),
            // Exhibition booths admin CRUD (Mockup page 22).
            new("Module.Booths", "/admin/booths", RequiredPermission: PermissionCatalog.Booths.View, Icon: "store"),
            // Sponsors admin CRUD (Mockup page 23).
            new("Module.Sponsors", "/admin/sponsors", RequiredPermission: PermissionCatalog.Sponsors.View, Icon: "award"),
            // 2D venue map editor (FR-605, FDS-006 §5.3).
            new("Module.VenueMap", "/admin/venue-map", RequiredPermission: PermissionCatalog.VenueMap.View, Icon: "map"),
        ]),
        new("Nav.Engagement",
        [
            // cp-stub-modules (Q4, 2026-07-30) — the last remaining ModulePlaceholder
            // stub. An IsStub item used to carry RequiredPermission: null, which made
            // it visible to EVERY signed-in admin regardless of role; the "Soon" badge
            // is not a gate. It now sits behind the same programme-read permission the
            // console it stands in for would need, so it is only advertised to the
            // operators who would eventually be able to open it.
            new("Module.LiveSessions", "/m/live-sessions", IsStub: true, RequiredPermission: PermissionCatalog.Sessions.View, Icon: "video"),
            // Submitted ratings read-only view + KPIs.
            new("Module.Ratings", "/admin/ratings", RequiredPermission: PermissionCatalog.Ratings.View, Icon: "star"),
            // Dynamic rating configuration — types / question groups / questions.
            new("Module.RatingConfig", "/admin/rating-config", RequiredPermission: PermissionCatalog.RatingConfig.View, Icon: "sliders"),
        ]),
        new("Nav.Knowledge",
        [
            // FAQ management (two-level group → entry).
            new("Module.Faq", "/admin/faq", RequiredPermission: PermissionCatalog.Faq.View, Icon: "help-circle"),
            // Centralised AI module: prompt
            // catalogue + invocations log. Real pages, no longer stubs.
            // CP Phase-1 — the AI Control Center dashboard (24h invocation health).
            new("Module.AiDashboard", "/admin/ai", RequiredPermission: PermissionCatalog.AiDashboard.View, Icon: "bar-chart"),
            // CP Phase-1 — the per-service "AI services" view (aggregates the
            // catalogue by feature); gated by the same AiPrompts.View.
            new("Module.AiServices", "/admin/ai/services", RequiredPermission: PermissionCatalog.AiPrompts.View, Icon: "list"),
            new("Module.AiPrompts", "/admin/ai/prompts", RequiredPermission: PermissionCatalog.AiPrompts.View, Icon: "sparkle"),
            new("Module.AiInvocations", "/admin/ai/invocations", RequiredPermission: PermissionCatalog.AiInvocations.View, Icon: "list-tree"),
        ]),
        new("Nav.Content",
        [
            // Centralised media library: manage every unified media asset.
            new("Module.MediaLibrary", "/admin/media-library", RequiredPermission: PermissionCatalog.MediaLibrary.View, Icon: "grid"),
            // Dynamic content CMS (PDF §1, §2.1).
            new("Module.ContentBlocks", "/admin/content-blocks", RequiredPermission: PermissionCatalog.ContentBlocks.View, Icon: "layout"),
            new("Module.Banners", "/admin/banners", RequiredPermission: PermissionCatalog.Banners.View, Icon: "image"),
            // Media gallery admin CRUD (Mockup page 30).
            new("Module.Media", "/admin/media", RequiredPermission: PermissionCatalog.Media.View, Icon: "film"),
            // News admin CRUD (Mockup screen 29).
            new("Module.News", "/admin/news", RequiredPermission: PermissionCatalog.News.View, Icon: "newspaper"),
            // Media partners admin CRUD (Mockup page 31).
            new("Module.MediaPartners", "/admin/media-partners", RequiredPermission: PermissionCatalog.MediaPartners.View, Icon: "share"),
            // Past-editions / archive admin CRUD (Mockup screen 24).
            new("Module.PreviousEditions", "/admin/archive", RequiredPermission: PermissionCatalog.Archive.View, Icon: "archive"),
        ]),
        // The broadcast-Notifications module (admin → audience) is the
        // Announcements desk below; the operator notification inbox lives at
        // /account/notifications (via the bell), not the nav.
        new("Nav.PublicRelations",
        [
            // Public-relations desk: invitation CRUD +
            // VIP list + bulk-notify (PDF §2.7.3).
            new("Module.Invitations", "/admin/invitations", RequiredPermission: PermissionCatalog.Invitations.View, Icon: "mail"),
            new("Module.Vips", "/admin/vips", RequiredPermission: PermissionCatalog.Vips.View, Icon: "crown"),
            // Admin broadcast desk: notify a session's registered attendees
            // or a broad audience (in-app + email), background-processed.
            // §6.16 (NAV-007) — the nav gate was Announcements.Send while the page
            // gate is Announcements.View, so a role granted View alone got no menu
            // item for a page it was entitled to open.
            new("Module.Announcements", "/admin/announcements", RequiredPermission: PermissionCatalog.Announcements.View, Icon: "send"),
            // Contact-us inbox (Figma 1388:7567) — triage app-submitted inquiries.
            new("Module.ContactInquiries", "/admin/contact-inquiries", RequiredPermission: PermissionCatalog.ContactInquiries.View, Icon: "inbox"),
        ]),
        new("Nav.Gates",
        [
            // Gate Module: master CRUD + role-adaptive operator console
            // (SIMF-FDS-003 §5.6 / SIMF-API-GATES-001).
            new("Module.Gates", "/admin/gates", RequiredPermission: PermissionCatalog.Gates.Manage, Icon: "door"),
            new("Module.GatesOperator", "/admin/gates/operator", RequiredPermission: PermissionCatalog.Gates.Operate, Icon: "scan"),
            // Hall-door arrival console (operator scans badge QR).
            new("Module.HallArrivals", "/admin/hall-arrivals", RequiredPermission: PermissionCatalog.HallArrivals.View, Icon: "log-in"),
            // Read-only gates operations dashboard over existing gate reports.
            new("Module.GatesDashboard", "/admin/gates/dashboard", RequiredPermission: PermissionCatalog.Gates.Manage, Icon: "bar-chart"),
        ]),
        new("Nav.ReferenceData",
        [
            new("Module.AdminInterests", "/admin/interests", RequiredPermission: PermissionCatalog.Interests.View, Icon: "tag"),
            // Country lookup admin CRUD.
            new("Module.AdminCountries", "/admin/countries", RequiredPermission: PermissionCatalog.Countries.View, Icon: "globe"),
            // Saudi-companies lookup (gov Excel import) feeding the visitor الجهة picker.
            new("Module.Organisations", "/admin/organisations", RequiredPermission: PermissionCatalog.Organisations.View, Icon: "building"),
            // Administrative-regions lookup (the 13 official Saudi regions), seeded from SaudiRegions.All.
            new("Module.Regions", "/admin/regions", RequiredPermission: PermissionCatalog.Regions.View, Icon: "map"),
            // Admin-managed lookup CRUD for ProfileType (per UserType).
            new("Module.AdminVisitorProfileTypes", "/admin/profile-types/visitor", RequiredPermission: PermissionCatalog.ProfileTypes.View, Icon: "list"),
            new("Module.AdminOtherProfileTypes", "/admin/profile-types/other", RequiredPermission: PermissionCatalog.ProfileTypes.View, Icon: "list"),
        ]),
        new("Nav.System",
        [
            // System Configuration (FDS-012 §5.5). Collapses the
            // former /m/configuration + /m/settings stubs into one real page.
            new("Module.Configuration", "/admin/configuration", RequiredPermission: PermissionCatalog.Configuration.View, Icon: "settings"),
            // Labelled Site Settings page (registration message + social links).
            // §6.16 (NAV-006) — the nav gate was Configuration.View while the page
            // gate is Configuration.Edit, so a read-only role saw the menu item and
            // was bounced by the page. The menu now promises exactly what the page
            // will honour.
            new("Module.SiteSettings", "/admin/site-settings", RequiredPermission: PermissionCatalog.Configuration.Edit, Icon: "globe"),
            // Transactional email-template editor (subject/body per identity email).
            new("Module.EmailTemplates", "/admin/email/templates", RequiredPermission: PermissionCatalog.EmailTemplates.View, Icon: "mail"),
            // Organization / About profile (edition-generic forum config).
            new("Module.OrganizationProfile", "/admin/organization-profile", RequiredPermission: PermissionCatalog.OrganizationProfile.View, Icon: "building"),
            new("Module.AdminLogs", "/admin/logs", RequiredPermission: PermissionCatalog.Logs.View, Icon: "file-text"),
            // Background-services monitor: live health of the in-process hosted workers.
            new("Module.ServicesMonitor", "/admin/ops/services", RequiredPermission: PermissionCatalog.ServicesMonitor.View, Icon: "bar-chart"),
            // Operation log viewer over the existing
            // OperationLogEntry table (no migration).
            new("Module.OperationLog", "/admin/operation-log", RequiredPermission: PermissionCatalog.OperationLog.View, Icon: "list-tree"),
            // Registration gate + archive visibility
            // singleton toggles (PDF §2.3, §2.4).
            new("Module.OperationsToggles", "/admin/operations", RequiredPermission: PermissionCatalog.Operations.View, Icon: "sliders"),
        ]),
        // Reporting module — date-ranged, exportable read-only reports. Each
        // report gates on its own permission so an operator can be given the
        // gate log without the attendee roster.
        new("Nav.Reports",
        [
            new("Module.ReportsHub", "/admin/reports", RequiredPermission: PermissionCatalog.Reports.View, Icon: "table"),
            new("Module.ReportsAttendance", "/admin/reports/attendance", RequiredPermission: PermissionCatalog.Reports.Attendance, Icon: "user-check"),
            new("Module.ReportsRegistrations", "/admin/reports/registrations", RequiredPermission: PermissionCatalog.Reports.Registrations, Icon: "users"),
            new("Module.ReportsGates", "/admin/reports/gates", RequiredPermission: PermissionCatalog.Reports.Gates, Icon: "shield"),
            new("Module.ReportsSessions", "/admin/reports/sessions", RequiredPermission: PermissionCatalog.Reports.Sessions, Icon: "presentation"),
            new("Module.ReportsRatings", "/admin/reports/ratings", RequiredPermission: PermissionCatalog.Reports.Ratings, Icon: "star"),
            new("Module.ReportsPartners", "/admin/reports/partners", RequiredPermission: PermissionCatalog.Reports.Partners, Icon: "handshake"),
            new("Module.ReportsMeetings", "/admin/reports/meetings", RequiredPermission: PermissionCatalog.Reports.Meetings, Icon: "calendar"),
            new("Module.ReportsEngagement", "/admin/reports/engagement", RequiredPermission: PermissionCatalog.Reports.Engagement, Icon: "message-circle"),
        ]),
    ];

    /// <summary>The label key for a module route, or <c>null</c> if it is not a known module.</summary>
    public static string? LabelKeyForHref(string href)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                if (string.Equals(item.Href, href, StringComparison.OrdinalIgnoreCase))
                {
                    return item.LabelKey;
                }
            }
        }
        return null;
    }
}
