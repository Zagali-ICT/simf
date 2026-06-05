// Tests: SIMF.ControlPanel.Tests/CpNavigationTests.cs
using SIMF.Common;
using SIMF.Common.Enums;

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
    /// signed-in user (the dashboard and the not-yet-built stubs).</summary>
    public sealed record NavItem(
        string LabelKey, string Href, bool IsStub = false, string? RequiredPermission = null);

    /// <summary>One navigation group — a heading and its items.</summary>
    public sealed record NavGroup(string LabelKey, IReadOnlyList<NavItem> Items);

    /// <summary>The navigation groups, in display order.</summary>
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("Nav.Overview",
        [
            new("Module.Dashboard", "/"),
            // D-202 Track-2 — read-only live-counts statistics dashboard.
            new("Module.Statistics", "/admin/statistics", RequiredPermission: PermissionCatalog.Statistics.View),
            // FR-506 — read-only session-attendance dashboard over HallAttendance (D-241).
            new("Module.Attendance", "/admin/attendance", RequiredPermission: PermissionCatalog.Attendance.View),
        ]),
        new("Nav.People",
        [
            // D-134 Sprint A — combined attendee roster over Visitors +
            // Others (read-only join on existing tables; no migration).
            new("Module.Attendees", "/admin/attendees", RequiredPermission: PermissionCatalog.Attendees.View),
            // P7e (D-055) — Visitor + Other attendee accounts and their pending
            // approval queues. Moved here from the old 23-item System group so the
            // people-management pages live together (the per-type Pending queues
            // deliver the registration approvals; Admins' own queue stays under
            // Access control).
            new("Module.AdminVisitors", "/admin/visitors", RequiredPermission: PermissionCatalog.Visitors.View),
            new("Module.AdminVisitorsPending", "/admin/visitors/pending", RequiredPermission: PermissionCatalog.Visitors.View),
            new("Module.AdminOthers", "/admin/others", RequiredPermission: PermissionCatalog.Others.View),
            new("Module.AdminOthersPending", "/admin/others/pending", RequiredPermission: PermissionCatalog.Others.View),
            // D-130 — print-bag station: lookup by QR id + reprint badge.
            new("Module.PrintBag", "/admin/print-bag", RequiredPermission: PermissionCatalog.Attendees.PrintBag),
        ]),
        // Issue-1 — access control grouped together: the admin accounts that
        // sign in to the CP, the roles & permissions they hold, and 2FA reset.
        new("Nav.AccessControl",
        [
            new("Module.AdminAdmins", "/admin/admins", RequiredPermission: PermissionCatalog.Admins.View),
            new("Module.AdminAdminsPending", "/admin/admins/pending", RequiredPermission: PermissionCatalog.Admins.View),
            // D-134 Sprint A / Issue-1 — roles + their per-page/per-action grants.
            new("Module.Roles", "/admin/roles", RequiredPermission: PermissionCatalog.Roles.View),
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa", RequiredPermission: PermissionCatalog.Admins.ResetTwoFactor),
        ]),
        new("Nav.Programme",
        [
            // D-134 Sprint B (D-135) — programme themes (SIMF-FDS-004 §5.1).
            new("Module.Themes", "/admin/themes", RequiredPermission: PermissionCatalog.Themes.View),
            // D-165 (gap doc G3) — programme sessions (SIMF-FDS-004 §5.3 + PDF §2.9).
            new("Module.Sessions", "/admin/sessions", RequiredPermission: PermissionCatalog.Sessions.View),
            // B9b (D-226) — dynamic session-category lookup (SIMF-FDS-004 §5.4).
            new("Module.SessionCategories", "/admin/session-categories", RequiredPermission: PermissionCatalog.SessionCategories.View),
            // Read-only run-of-show timeline over the existing sessions list.
            new("Module.ProgrammeTimeline", "/admin/programme/timeline", RequiredPermission: PermissionCatalog.ProgrammeTimeline.View),
            // D-134 Sprint B (D-135) — venue halls (SIMF-FDS-004 §5.2).
            new("Module.Halls", "/admin/halls", RequiredPermission: PermissionCatalog.Halls.View),
            // D-182 (CP UI for D-175 seat reservations) — hall seat
            // layout editor + per-session seat plan.
            new("Module.HallSeatLayouts", "/admin/halls/seat-layouts", RequiredPermission: PermissionCatalog.SeatLayouts.View),
            new("Module.SessionSeatPlans", "/admin/sessions/seat-plans", RequiredPermission: PermissionCatalog.SeatPlans.View),
            // D-269 — attendee meeting requests TO a speaker (Mockup page 20).
            new("Module.SpeakerMeetingRequests", "/admin/speaker-meeting-requests", RequiredPermission: PermissionCatalog.SpeakerMeetingRequests.View),
            // D-153 — programme speakers (SIMF-DAT-001 §5.4).
            new("Module.Speakers", "/admin/speakers", RequiredPermission: PermissionCatalog.Speakers.View),
            // P2.3 (D-228) — speaker presentation files (FR-407). Reuses Speakers.*.
            new("Module.SpeakerPresentations", "/admin/speaker-presentations", RequiredPermission: PermissionCatalog.Speakers.View),
            // P2.2 (D-227) — booking approval queue (FDS-005 §5.2).
            new("Module.Bookings", "/admin/bookings", RequiredPermission: PermissionCatalog.Bookings.View),
            // SIMF-FDS-013 (D-248) — flexible hall config: purpose + meeting tables
            // + hall allocations (whole / random-by-count / row-column).
            new("Module.MeetingTables", "/admin/meeting-tables", RequiredPermission: PermissionCatalog.MeetingTables.View),
            // SIMF-FDS-013 (D-248) — admin-arranged B2B/B2C business meetings.
            new("Module.BusinessMeetings", "/admin/business-meetings", RequiredPermission: PermissionCatalog.BusinessMeetings.View),
        ]),
        // Scientific committee desks (moved out of the old 23-item System group):
        // per-session moderator grants, the central Q&A queue, and the AI
        // session-summary / محضر desk — all session-bound, so they sit by Programme.
        new("Nav.ScientificCommittee",
        [
            // D-169 (gap doc G6) — per-session moderator grants admin CRUD
            // (PDF §2.7.2). The moderator's own live-queue desk is
            // /sessions/{id}/moderate — reached from the Sessions grid, not the nav.
            new("Module.SessionModerators", "/admin/session-moderators", RequiredPermission: PermissionCatalog.SessionModerators.View),
            // P3.3 (D-234) — Scientific-Committee central Q&A queue (stage 2).
            new("Module.QuestionQueue", "/admin/question-queue", RequiredPermission: PermissionCatalog.Questions.View),
            // P4.1 (D-238) — Scientific-Committee AI session-summary / محضر desk.
            new("Module.SessionSummaries", "/admin/session-summaries", RequiredPermission: PermissionCatalog.SessionSummaries.View),
        ]),
        new("Nav.Exhibition",
        [
            // P1.1 (D-214) — the "Exhibitors" stub was removed: exhibitor
            // onboarding is delivered by the real Exhibitors page (CP-only
            // exhibitor + account provisioning, D-202) plus Booths and Sponsors;
            // in-app exhibitor self-signup was permanently descoped (D-199/D-202).
            // D-202 Track-2 — exhibitor CRUD + account provisioning.
            new("Module.Exhibitors", "/admin/exhibitors", RequiredPermission: PermissionCatalog.Exhibitors.View),
            // D-199 — Exhibition booths admin CRUD (Mockup page 22).
            new("Module.Booths", "/admin/booths", RequiredPermission: PermissionCatalog.Booths.View),
            // D-199 — sponsors admin CRUD (Mockup page 23).
            new("Module.Sponsors", "/admin/sponsors", RequiredPermission: PermissionCatalog.Sponsors.View),
            // P2.5 (D-230) — 2D venue map editor (FR-605, FDS-006 §5.3).
            new("Module.VenueMap", "/admin/venue-map", RequiredPermission: PermissionCatalog.VenueMap.View),
        ]),
        new("Nav.Engagement",
        [
            new("Module.LiveSessions", "/m/live-sessions", IsStub: true),
            // D-199 — audience-comments moderation desk (Mockup page 28).
            new("Module.Moderation", "/admin/comments-moderation", RequiredPermission: PermissionCatalog.Comments.View),
            // D-199 — forum ratings read-only view (Mockup screen 40).
            new("Module.Ratings", "/admin/ratings", RequiredPermission: PermissionCatalog.Ratings.View),
        ]),
        new("Nav.Knowledge",
        [
            // P2.1 (D-211) — FAQ management (two-level group → entry).
            new("Module.Faq", "/admin/faq", RequiredPermission: PermissionCatalog.Faq.View),
            // D-176 (gap doc G12) — centralised AI module: prompt
            // catalogue + invocations log. Real pages, no longer stubs.
            new("Module.AiPrompts", "/admin/ai/prompts", RequiredPermission: PermissionCatalog.AiPrompts.View),
            new("Module.AiInvocations", "/admin/ai/invocations", RequiredPermission: PermissionCatalog.AiInvocations.View),
        ]),
        new("Nav.Content",
        [
            // D-173 (gap doc G8) — Dynamic content CMS (PDF §1, §2.1).
            new("Module.ContentBlocks", "/admin/content-blocks", RequiredPermission: PermissionCatalog.ContentBlocks.View),
            new("Module.Banners", "/admin/banners", RequiredPermission: PermissionCatalog.Banners.View),
            // D-199 — Media gallery admin CRUD (Mockup page 30).
            new("Module.Media", "/admin/media", RequiredPermission: PermissionCatalog.Media.View),
            // D-199 — News admin CRUD (Mockup screen 29).
            new("Module.News", "/admin/news", RequiredPermission: PermissionCatalog.News.View),
            // D-199 — media partners admin CRUD (Mockup page 31).
            new("Module.MediaPartners", "/admin/media-partners", RequiredPermission: PermissionCatalog.MediaPartners.View),
            // D-199 — past-editions / archive admin CRUD (Mockup screen 24).
            new("Module.PreviousEditions", "/admin/archive", RequiredPermission: PermissionCatalog.Archive.View),
        ]),
        // D-132 — the broadcast-Notifications module (admin → audience) is not
        // built yet; the operator notification inbox lives at
        // /account/notifications (via the bell), not the nav.
        new("Nav.PublicRelations",
        [
            // D-168 (gap doc G5) — public-relations desk: invitation CRUD +
            // VIP list + bulk-notify (PDF §2.7.3).
            new("Module.Invitations", "/admin/invitations", RequiredPermission: PermissionCatalog.Invitations.View),
            new("Module.Vips", "/admin/vips", RequiredPermission: PermissionCatalog.Vips.View),
        ]),
        new("Nav.Gates",
        [
            // D-148 — Gate Module: master CRUD + role-adaptive operator console
            // (SIMF-FDS-003 §5.6 / SIMF-API-GATES-001).
            new("Module.Gates", "/admin/gates", RequiredPermission: PermissionCatalog.Gates.Manage),
            new("Module.GatesOperator", "/admin/gates/operator", RequiredPermission: PermissionCatalog.Gates.Operate),
            // P5.1d — D-244: hall-door arrival console (operator scans badge QR).
            new("Module.HallArrivals", "/admin/hall-arrivals", RequiredPermission: PermissionCatalog.HallArrivals.View),
            // Read-only gates operations dashboard over existing gate reports.
            new("Module.GatesDashboard", "/admin/gates/dashboard", RequiredPermission: PermissionCatalog.Gates.Manage),
        ]),
        new("Nav.ReferenceData",
        [
            new("Module.AdminInterests", "/admin/interests", RequiredPermission: PermissionCatalog.Interests.View),
            // D-155 — country lookup admin CRUD (D-151 / D-152 reference data).
            new("Module.AdminCountries", "/admin/countries", RequiredPermission: PermissionCatalog.Countries.View),
            // B3 (D-220) — Saudi-companies lookup (gov Excel import) feeding the visitor الجهة picker.
            new("Module.Organisations", "/admin/organisations", RequiredPermission: PermissionCatalog.Organisations.View),
            // SIMF-FDS-014 (D-281/C2) — shared Contact directory reused by Sponsors /
            // Exhibitors / MediaPartners / Speakers / Booth officers.
            new("Module.Contacts", "/admin/contacts", RequiredPermission: PermissionCatalog.Contacts.View),
            // D-118 — admin-managed lookup CRUD for ProfileType (per UserType).
            new("Module.AdminVisitorProfileTypes", "/admin/profile-types/visitor", RequiredPermission: PermissionCatalog.ProfileTypes.View),
            new("Module.AdminOtherProfileTypes", "/admin/profile-types/other", RequiredPermission: PermissionCatalog.ProfileTypes.View),
        ]),
        new("Nav.System",
        [
            // P2.4 (D-229) — System Configuration (FDS-012 §5.5). Collapses the
            // former /m/configuration + /m/settings stubs into one real page.
            new("Module.Configuration", "/admin/configuration", RequiredPermission: PermissionCatalog.Configuration.View),
            new("Module.AdminLogs", "/admin/logs", RequiredPermission: PermissionCatalog.Logs.View),
            // D-134 Sprint A — Operation log viewer over the existing
            // OperationLogEntry table (no migration).
            new("Module.OperationLog", "/admin/operation-log", RequiredPermission: PermissionCatalog.OperationLog.View),
            // D-166 (gap doc G4) — registration gate + archive visibility
            // singleton toggles (PDF §2.3, §2.4).
            new("Module.OperationsToggles", "/admin/operations", RequiredPermission: PermissionCatalog.Operations.View),
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
