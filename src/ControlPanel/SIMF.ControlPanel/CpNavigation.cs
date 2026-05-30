// Tests: SIMF.ControlPanel.Tests/CpNavigationTests.cs
using SIMF.Common.Enums;

namespace SIMF.ControlPanel;

/// <summary>
/// The Control Panel navigation map — the eight groups and their modules
/// (SIMF-CPD-001 section 5.1; D-132 removed the standalone Notifications
/// group). Each label is a resource key resolved through
/// <see cref="Strings" />. Permission filtering of this map is gated on the
/// per-type permission map (SIMF-CPD-001 OI-3 / gate D1) and is not applied yet.
/// </summary>
public static class CpNavigation
{
    /// <summary>One navigation item — a module link. <c>IsStub</c> marks
    /// entries that currently resolve to <c>ModulePlaceholder</c>; the shell
    /// renders a "Soon" badge so operators know which menu items are real
    /// (D-132).</summary>
    public sealed record NavItem(string LabelKey, string Href, bool IsStub = false);

    /// <summary>One navigation group — a heading and its items.</summary>
    public sealed record NavGroup(string LabelKey, IReadOnlyList<NavItem> Items);

    /// <summary>The eight navigation groups, in display order.</summary>
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("Nav.Overview",
        [
            new("Module.Dashboard", "/"),
            // D-202 Track-2 — read-only live-counts statistics dashboard.
            new("Module.Statistics", "/admin/statistics"),
        ]),
        new("Nav.People",
        [
            new("Module.RegistrationRequests", "/m/registration-requests", IsStub: true),
            // D-134 Sprint A — combined attendee roster over Visitors +
            // Others (read-only join on existing tables; no migration).
            new("Module.Attendees", "/admin/attendees"),
            // D-130 — print-bag station: lookup by QR id + reprint badge.
            new("Module.PrintBag", "/admin/print-bag"),
            // D-134 Sprint A — Roles module shipped against the existing
            // SimfRole + Permission + RolePermission entities (no migration).
            new("Module.Roles", "/admin/roles"),
        ]),
        new("Nav.Programme",
        [
            // D-134 Sprint B (D-135) — programme themes (SIMF-FDS-004 §5.1).
            new("Module.Themes", "/admin/themes"),
            // D-165 (gap doc G3) — programme sessions (SIMF-FDS-004 §5.3 + PDF §2.9).
            new("Module.Sessions", "/admin/sessions"),
            // Read-only run-of-show timeline over the existing sessions list.
            new("Module.ProgrammeTimeline", "/admin/programme/timeline"),
            // D-134 Sprint B (D-135) — venue halls (SIMF-FDS-004 §5.2).
            new("Module.Halls", "/admin/halls"),
            // D-182 (CP UI for D-175 seat reservations) — hall seat
            // layout editor + per-session seat plan.
            new("Module.HallSeatLayouts", "/admin/halls/seat-layouts"),
            new("Module.SessionSeatPlans", "/admin/sessions/seat-plans"),
            // D-183 (CP UI for D-174 delegations + meeting requests).
            new("Module.Delegations", "/admin/delegations"),
            new("Module.MeetingRequests", "/admin/meeting-requests"),
            // D-153 — programme speakers (SIMF-DAT-001 §5.4).
            new("Module.Speakers", "/admin/speakers"),
            new("Module.Bookings", "/m/bookings", IsStub: true),
        ]),
        new("Nav.Exhibition",
        [
            new("Module.Exhibitors", "/m/exhibitors", IsStub: true),
            // D-202 Track-2 — exhibitor / sponsor company CRUD + account provisioning.
            new("Module.Companies", "/admin/companies"),
            // D-199 — Exhibition booths admin CRUD (Mockup page 22).
            new("Module.Booths", "/admin/booths"),
            // D-199 — sponsors admin CRUD (Mockup page 23).
            new("Module.Sponsors", "/admin/sponsors"),
            new("Module.VenueMap", "/m/venue-map", IsStub: true),
        ]),
        new("Nav.Engagement",
        [
            new("Module.LiveSessions", "/m/live-sessions", IsStub: true),
            // D-199 — audience-comments moderation desk (Mockup page 28).
            new("Module.Moderation", "/admin/comments-moderation"),
            // D-199 — forum ratings read-only view (Mockup screen 40).
            new("Module.Ratings", "/admin/ratings"),
        ]),
        new("Nav.Knowledge",
        [
            new("Module.Faq", "/m/faq", IsStub: true),
            // D-176 (gap doc G12) — centralised AI module: prompt
            // catalogue + invocations log. Real pages, no longer stubs.
            new("Module.AiPrompts", "/admin/ai/prompts"),
            new("Module.AiInvocations", "/admin/ai/invocations"),
        ]),
        new("Nav.Content",
        [
            // D-173 (gap doc G8) — Dynamic content CMS (PDF §1, §2.1).
            new("Module.ContentBlocks", "/admin/content-blocks"),
            new("Module.Banners", "/admin/banners"),
            // D-199 — Media gallery admin CRUD (Mockup page 30).
            new("Module.Media", "/admin/media"),
            // D-199 — News admin CRUD (Mockup screen 29).
            new("Module.News", "/admin/news"),
            // D-199 — media partners admin CRUD (Mockup page 31).
            new("Module.MediaPartners", "/admin/media-partners"),
            // D-199 — past-editions / archive admin CRUD (Mockup screen 24).
            new("Module.PreviousEditions", "/admin/archive"),
        ]),
        // D-132 — the broadcast-Notifications module (admin → audience)
        // is not built yet; the existing operator notification inbox lives
        // at /account/notifications (via the bell). Removed the duplicate
        // "Notifications" nav stub that was misleading operators into
        // the placeholder instead of the real inbox.
        new("Nav.System",
        [
            // P7e — D-055: three pairs of pages, one per UserType
            // (Admin / Other / Visitor). The "Staff" label is gone —
            // P7c renamed the API; P7e finishes the rename on the CP UI.
            new("Module.AdminAdmins", "/admin/admins"),
            new("Module.AdminAdminsPending", "/admin/admins/pending"),
            new("Module.AdminOthers", "/admin/others"),
            new("Module.AdminOthersPending", "/admin/others/pending"),
            new("Module.AdminVisitors", "/admin/visitors"),
            new("Module.AdminVisitorsPending", "/admin/visitors/pending"),
            new("Module.AdminInterests", "/admin/interests"),
            // D-155 — country lookup admin CRUD (D-151 / D-152 reference data).
            new("Module.AdminCountries", "/admin/countries"),
            // D-118 — admin-managed lookup CRUD for ProfileType (per UserType).
            new("Module.AdminVisitorProfileTypes", "/admin/profile-types/visitor"),
            new("Module.AdminOtherProfileTypes", "/admin/profile-types/other"),
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa"),
            new("Module.AdminLogs", "/admin/logs"),
            // D-148 — Gate Module: master CRUD + role-adaptive operator console
            // (SIMF-FDS-003 §5.6 / SIMF-API-GATES-001).
            new("Module.Gates", "/admin/gates"),
            new("Module.GatesOperator", "/admin/gates/operator"),
            // Read-only gates operations dashboard over existing gate reports.
            new("Module.GatesDashboard", "/admin/gates/dashboard"),
            new("Module.Configuration", "/m/configuration", IsStub: true),
            // D-134 Sprint A — Operation log viewer over the existing
            // OperationLogEntry table (no migration).
            new("Module.OperationLog", "/admin/operation-log"),
            // D-166 (gap doc G4) — registration gate + archive visibility
            // singleton toggles (PDF §2.3, §2.4).
            new("Module.OperationsToggles", "/admin/operations"),
            // D-168 (gap doc G5) — public-relations desk: invitation CRUD +
            // VIP list + bulk-notify (PDF §2.7.3). Open item G-OI-4 was
            // resolved auto-mode to share the System group instead of a
            // separate PR layout.
            new("Module.Invitations", "/admin/invitations"),
            new("Module.Vips", "/admin/vips"),
            // D-169 (gap doc G6) — per-session moderator grants admin CRUD
            // (PDF §2.7.2). The session moderator's own live-queue desk
            // is /sessions/{id}/moderate — accessed from the Sessions
            // grid, not the nav.
            new("Module.SessionModerators", "/admin/session-moderators"),
            new("Module.Settings", "/m/settings", IsStub: true),
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
