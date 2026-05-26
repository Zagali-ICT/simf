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
    /// <summary>One navigation item — a module link.</summary>
    public sealed record NavItem(string LabelKey, string Href);

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
            new("Module.RegistrationRequests", "/m/registration-requests"),
            new("Module.Attendees", "/m/attendees"),
            new("Module.Roles", "/m/roles"),
        ]),
        new("Nav.Programme",
        [
            new("Module.Themes", "/m/themes"),
            new("Module.Sessions", "/m/sessions"),
            new("Module.Halls", "/m/halls"),
            new("Module.Speakers", "/m/speakers"),
            new("Module.Bookings", "/m/bookings"),
        ]),
        new("Nav.Exhibition",
        [
            new("Module.Exhibitors", "/m/exhibitors"),
            new("Module.Booths", "/m/booths"),
            new("Module.Sponsors", "/m/sponsors"),
            new("Module.VenueMap", "/m/venue-map"),
        ]),
        new("Nav.Engagement",
        [
            new("Module.LiveSessions", "/m/live-sessions"),
            new("Module.Moderation", "/m/moderation"),
        ]),
        new("Nav.Knowledge",
        [
            new("Module.Faq", "/m/faq"),
            new("Module.AiSettings", "/m/ai-settings"),
        ]),
        new("Nav.Content",
        [
            new("Module.Media", "/m/media"),
            new("Module.News", "/m/news"),
            new("Module.PreviousEditions", "/m/previous-editions"),
        ]),
        new("Nav.Communications",
        [
            new("Module.Notifications", "/m/notifications"),
        ]),
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
            new("Module.AdminResetTwoFactor", "/admin/reset-2fa"),
            new("Module.AdminLogs", "/admin/logs"),
            new("Module.Configuration", "/m/configuration"),
            new("Module.OperationLog", "/m/operation-log"),
            new("Module.Settings", "/m/settings"),
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
