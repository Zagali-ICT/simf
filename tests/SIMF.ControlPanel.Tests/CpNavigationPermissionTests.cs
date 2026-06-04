// Issue-1 — the side menu carries a permission per item and the shell filters
// on it. These guard the wiring: every gate references a real catalogue code,
// and every real /admin item is gated (the dashboard is the only exception).
using SIMF.Common;
using SIMF.ControlPanel;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpNavigationPermissionTests
{
    [Fact]
    public void Every_nav_required_permission_is_a_real_catalogue_code()
    {
        var valid = PermissionCatalog.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(CpNavigation.Groups.SelectMany(group => group.Items), item =>
        {
            if (item.RequiredPermission is not null)
            {
                Assert.Contains(item.RequiredPermission, valid);
            }
        });
    }

    [Fact]
    public void Every_real_admin_nav_item_is_permission_gated()
    {
        // Non-stub items that target an /admin route must carry a permission so
        // the shell can filter them; "/" (dashboard) is the ungated exception.
        var ungatedAdminItems = CpNavigation.Groups
            .SelectMany(group => group.Items)
            .Where(item => !item.IsStub
                && item.Href.StartsWith("/admin", StringComparison.Ordinal)
                && item.RequiredPermission is null)
            .Select(item => item.Href)
            .ToList();

        Assert.Empty(ungatedAdminItems);
    }
}
