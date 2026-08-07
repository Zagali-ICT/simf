// Issue-1 — the side menu carries a permission per item and the shell filters
// on it. These guard the wiring: every gate references a real catalogue code,
// and every real /admin item is gated (the dashboard is the only exception).
using System.Text.RegularExpressions;
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

    [Fact]
    public void Every_nav_gate_matches_the_gate_on_the_page_it_links_to()
    {
        // §6.16 (NAV-006 / NAV-007) — the menu and the page each carried their own
        // permission, and two of them disagreed. Both directions are defects, and
        // they fail in opposite ways:
        //
        //   nav WEAKER than page (NAV-006, /admin/site-settings: nav wanted
        //     Configuration.View, the page demands Configuration.Edit) — a
        //     read-only role sees the menu item and is bounced when it clicks.
        //   nav STRICTER than page (NAV-007, /admin/announcements: nav wanted
        //     Announcements.Send, the page only needs Announcements.View) — a role
        //     granted View gets no menu item for a page it may open, so the
        //     feature is invisible to exactly the people entitled to it.
        //
        // Neither shows up in a render test: the super-admin's "*" wildcard
        // satisfies both sides, so every gate looks correct to the seeded admin.
        var codeForExpression = PermissionExpressionMap();
        var mismatches = new List<string>();

        foreach (var item in CpNavigation.Groups.SelectMany(group => group.Items))
        {
            if (item.IsStub) continue;

            var pageGate = PageGateFor(item.Href, codeForExpression);
            if (pageGate is null) continue;   // no [RequirePermission] on the page

            if (!string.Equals(pageGate, item.RequiredPermission, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{item.Href}: nav={item.RequiredPermission ?? "(none)"} page={pageGate}");
            }
        }

        Assert.True(mismatches.Count == 0,
            "§6.16 (NAV-006/NAV-007): the side menu must promise exactly what the "
            + "page will honour — " + string.Join("; ", mismatches));
    }

    [Fact]
    public void Every_stub_nav_item_is_permission_gated()
    {
        // cp-stub-modules (Q4) — an IsStub item used to carry
        // RequiredPermission: null, which the two facts above deliberately skip.
        // The "Soon" pill is a label, not a gate: with a null permission the
        // shell showed the item to EVERY signed-in admin, and the placeholder it
        // links to was reachable by all of them. A stub is a module that is
        // coming, not a module that is public — it is gated on the permission
        // the finished console will need.
        var ungatedStubs = CpNavigation.Groups
            .SelectMany(group => group.Items)
            .Where(item => item.IsStub && item.RequiredPermission is null)
            .Select(item => item.Href)
            .ToList();

        Assert.True(ungatedStubs.Count == 0,
            "cp-stub-modules: these stub nav items carry no permission, so every "
            + "signed-in admin sees them regardless of role: "
            + string.Join(", ", ungatedStubs));
    }

    [Fact]
    public void Every_stub_nav_gate_matches_the_gate_on_the_placeholder_that_serves_it()
    {
        // The stub routes are served by a PARAMETERISED page (/m/{Module}), which
        // the literal-route lookup used by the fact above cannot find — which is
        // why stubs were skipped there, and why the nav could be gated while the
        // page stayed open. Gating one half only moves the hole: the item leaves
        // the menu but the URL still opens for anyone who types it.
        var codeForExpression = PermissionExpressionMap();
        var mismatches = new List<string>();

        foreach (var item in CpNavigation.Groups.SelectMany(group => group.Items))
        {
            if (!item.IsStub) continue;

            var pageGate = TemplatePageGateFor(item.Href, codeForExpression);
            if (!string.Equals(pageGate, item.RequiredPermission, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{item.Href}: nav={item.RequiredPermission ?? "(none)"} "
                    + $"page={pageGate ?? "(none)"}");
            }
        }

        Assert.True(mismatches.Count == 0,
            "cp-stub-modules: the page serving a stub route must demand exactly the "
            + "permission its menu item promises — " + string.Join("; ", mismatches));
    }

    /// <summary>"PermissionCatalog.Sessions.Edit" → "Sessions.Edit", built by
    /// reflecting the catalogue's nested classes so the map cannot drift.</summary>
    private static Dictionary<string, string> PermissionExpressionMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var nested in typeof(PermissionCatalog).GetNestedTypes())
        {
            foreach (var field in nested.GetFields())
            {
                if (field is { IsLiteral: true, IsInitOnly: false }
                    && field.GetRawConstantValue() is string code)
                {
                    map[$"PermissionCatalog.{nested.Name}.{field.Name}"] = code;
                }
            }
        }
        return map;
    }

    /// <summary>The permission code on the .razor that serves this route, or null
    /// when the page carries no [RequirePermission].</summary>
    private static string? PageGateFor(string href, Dictionary<string, string> codes)
    {
        var root = FindRepoRoot();
        var pagesDir = Path.Combine(
            root, "src/ControlPanel/SIMF.ControlPanel/Components".Replace('/', Path.DirectorySeparatorChar));

        foreach (var file in Directory.EnumerateFiles(pagesDir, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!Regex.IsMatch(source, $@"@page\s+""{Regex.Escape(href)}""")) continue;

            var gate = Regex.Match(source,
                @"@attribute\s*\[RequirePermission\(\s*(PermissionCatalog\.\w+\.\w+)\s*\)\]");
            return gate.Success && codes.TryGetValue(gate.Groups[1].Value, out var code)
                ? code
                : null;
        }
        return null;
    }

    /// <summary>The permission code on the .razor whose route TEMPLATE serves this
    /// href (e.g. <c>/m/{Module}</c> serves <c>/m/live-sessions</c>), or null when
    /// no page matches or the matching page carries no
    /// <c>[RequirePermission]</c>.</summary>
    private static string? TemplatePageGateFor(string href, Dictionary<string, string> codes)
    {
        var root = FindRepoRoot();
        var pagesDir = Path.Combine(
            root, "src/ControlPanel/SIMF.ControlPanel/Components".Replace('/', Path.DirectorySeparatorChar));

        foreach (var file in Directory.EnumerateFiles(pagesDir, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var serves = false;
            foreach (Match route in Regex.Matches(source, @"^@page\s+""([^""]+)""", RegexOptions.Multiline))
            {
                if (RouteTemplateServes(route.Groups[1].Value, href))
                {
                    serves = true;
                    break;
                }
            }
            if (!serves) continue;

            var gate = Regex.Match(source,
                @"@attribute\s*\[RequirePermission\(\s*(PermissionCatalog\.\w+\.\w+)\s*\)\]");
            return gate.Success && codes.TryGetValue(gate.Groups[1].Value, out var code)
                ? code
                : null;
        }
        return null;
    }

    /// <summary>True when a route template answers a concrete href — same segment
    /// count, and each template segment is either a parameter or the identical
    /// literal.</summary>
    private static bool RouteTemplateServes(string template, string href)
    {
        var templateSegments = template.Trim('/').Split('/');
        var hrefSegments = href.Trim('/').Split('/');
        if (templateSegments.Length != hrefSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < templateSegments.Length; i++)
        {
            if (templateSegments[i].StartsWith('{'))
            {
                continue;
            }
            if (!string.Equals(templateSegments[i], hrefSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException("Could not locate the SIMF repo root.")
            : dir.FullName;
    }
}
