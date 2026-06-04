using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Tests;

/// <summary>Tests for the Control Panel navigation map.</summary>
public sealed class CpNavigationTests
{
    [Fact]
    public void There_are_nine_navigation_groups()
    {
        // D-132 removed the standalone Notifications group (9 → 8). Issue-1
        // then added the "Access control" group (Admins + Roles & permissions
        // + Reset 2FA, pulled together out of People/System), so the canonical
        // count is 9: Overview, People, Access control, Programme, Exhibition,
        // Engagement, Knowledge, Content, System.
        Assert.Equal(9, CpNavigation.Groups.Count);
    }

    [Fact]
    public void Every_module_route_is_unique()
    {
        var routes = CpNavigation.Groups.SelectMany(group => group.Items)
            .Select(item => item.Href)
            .ToList();

        Assert.Equal(routes.Count, routes.Distinct().Count());
    }

    [Theory]
    [InlineData("/", "Module.Dashboard")]
    // D-165 renamed /m/sessions → /admin/sessions; uppercase variant keeps
    // the case-insensitive lookup contract under test.
    [InlineData("/admin/sessions", "Module.Sessions")]
    [InlineData("/ADMIN/SESSIONS", "Module.Sessions")]
    // P7e — D-055: three-UserType admin pages.
    [InlineData("/admin/admins", "Module.AdminAdmins")]
    [InlineData("/admin/admins/pending", "Module.AdminAdminsPending")]
    [InlineData("/admin/others", "Module.AdminOthers")]
    [InlineData("/admin/others/pending", "Module.AdminOthersPending")]
    [InlineData("/admin/visitors", "Module.AdminVisitors")]
    [InlineData("/admin/visitors/pending", "Module.AdminVisitorsPending")]
    // D-182 (CP UI for D-175 seat reservations) — two new entries under
    // Nav.Programme. Pinned here so a future nav refactor renaming these
    // routes (the same shape that bit D-180) trips a real test.
    [InlineData("/admin/halls/seat-layouts", "Module.HallSeatLayouts")]
    [InlineData("/admin/sessions/seat-plans", "Module.SessionSeatPlans")]
    public void LabelKeyForHref_resolves_a_known_route(string href, string expectedKey)
    {
        Assert.Equal(expectedKey, CpNavigation.LabelKeyForHref(href));
    }

    [Fact]
    public void LabelKeyForHref_returns_null_for_an_unknown_route()
    {
        Assert.Null(CpNavigation.LabelKeyForHref("/m/does-not-exist"));
    }

    [Fact]
    public void Nav_does_not_contain_the_legacy_admin_staff_route()
    {
        // P7e — D-055: /admin/staff was renamed to /admin/admins.
        Assert.DoesNotContain(CpNavigation.Groups.SelectMany(g => g.Items),
            item => item.Href.Contains("/admin/staff", StringComparison.OrdinalIgnoreCase));
    }
}
