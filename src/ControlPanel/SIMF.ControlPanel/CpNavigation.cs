// Tests: SIMF.ControlPanel.Tests/CpNavigationTests.cs
using SIMF.Common.Enums;

namespace SIMF.ControlPanel;

/// <summary>
/// The Control Panel navigation map — the nine groups and their modules
/// (SIMF-CPD-001 section 5.1). Each label is a resource key resolved through
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

    /// <summary>The nine navigation groups, in display order.</summary>
    public static readonly IReadOnlyList<NavGroup> Groups =
    [
        new("Nav.Overview",
        [
            new("Module.Dashboard", "/"),
        ]),
        new("Nav.People",
        [
            new("Module.RegistrationRequests", "/m/registration-requests", IsStub: true),
            new("Module.Attendees", "/m/attendees", IsStub: true),
            // D-130 — print-bag station: lookup by QR id + reprint badge.
            new("Module.PrintBag", "/admin/print-bag"),
            // D-134 Sprint A — Roles module shipped against the existing
            // SimfRole + Permission + RolePermission entities (no migration).
            new("Module.Roles", "/admin/roles"),
        ]),
        new("Nav.Programme",
        [
            new("Module.Themes", "/m/themes", IsStub: true),
            new("Module.Sessions", "/m/sessions", IsStub: true),
            new("Module.Halls", "/m/halls", IsStub: true),
            new("Module.Speakers", "/m/speakers", IsStub: true),
            new("Module.Bookings", "/m/bookings", IsStub: true),
        ]),
        new("Nav.Exhibition",
        [
            new("Module.Exhibitors", "/m/exhibitors", IsStub: true),
            new("Module.Booths", "/m/booths", IsStub: true),
            new("Module.Sponsors", "/m/sponsors", IsStub: true),
            new("Module.VenueMap", "/m/venue-map", IsStub: true),
        ]),
        new("Nav.Engagement",
        [
            new("Module.LiveSessions", "/m/live-sessions", IsStub: true),
            new("Module.Moderation", "/m/moderation", IsStub: true),
        ]),
        new("Nav.Knowledge",
        [
            new("Module.Faq", "/m/faq", IsStub: true),
            new("Module.AiSettings", "/m/ai-settings", IsStub: true),
        ]),
        new("Nav.Content",
        [
            new("Module.Media", "/m/media", IsStub: true),
            new("Module.News", "/m/news", IsStub: true),
            new("Module.PreviousEditions", "/m/previous-editions", IsStub: true),
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
            // D-118 — admin-managed lookup CRUD for ProfileType (per UserType).
            new("Module.AdminVisitorProfileTypes", "/admin/profile-types/visitor"),
            new("Module.AdminOtherProfileTypes", "/admin/profile-types/other"),
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa"),
            new("Module.AdminLogs", "/admin/logs"),
            new("Module.Configuration", "/m/configuration", IsStub: true),
            // D-134 Sprint A — Operation log viewer over the existing
            // OperationLogEntry table (no migration).
            new("Module.OperationLog", "/admin/operation-log"),
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
