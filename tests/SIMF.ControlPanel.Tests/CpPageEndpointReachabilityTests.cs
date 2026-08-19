// A Control Panel page is only usable if the role that can OPEN it can also
// reach the endpoints it calls on load.
//
// Every other permission guard in this suite checks one half of that. The nav
// tests pin menu-gate against page-gate; PermissionEnforcementTests pins that
// each endpoint HAS a gate. Nothing pinned the seam between them, so a page
// could be gated on a permission one role holds while calling an endpoint gated
// on a permission that role does not hold - the page opens, its first fetch
// 403s, and the operator sees an empty screen with a toast.
//
// It is invisible to every other test because every fixture signs in as the
// seeded super-administrator, whose "*" wildcard satisfies both sides. It is
// invisible in manual QA for the same reason. It surfaces only for the role the
// page was actually built for, which is the one nobody signs in as.
//
// Static assertions over the checked-in .razor / .razor.cs and the endpoint
// Configure() blocks; no host is started and no page is rendered.
using System.Text.RegularExpressions;
using SIMF.Common;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class CpPageEndpointReachabilityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>Calls a page issues on a background timer or an explicit user
    /// action rather than on load, keyed by route, with the reason each is
    /// excluded.
    ///
    /// <para>Empty today, and that is the point of having it: the two lockouts
    /// this fixture was written for were both first-load fetches, and an
    /// allow-list that starts populated is one nobody reads. An entry here is a
    /// claim that the call cannot fire for a role that lacks the permission -
    /// which for anything behind <c>AuthorizedAction</c> is true, because the
    /// control is not rendered. Do NOT add a first-load fetch here to make the
    /// build green; that is the defect, not the noise.</para></summary>
    private static readonly Dictionary<string, string[]> NotOnLoad = new(StringComparer.Ordinal);

    /// <summary>Every (page, endpoint) pair where a role that can open the page
    /// cannot call the endpoint.
    ///
    /// <para>Scoped to pages whose gate a NON-administrator role actually holds.
    /// Administrator carries the wildcard, so a page only Administrator can open
    /// can never fail this way and would only produce noise.</para></summary>
    [Fact]
    public void Every_role_that_can_open_a_page_can_reach_the_endpoints_it_calls()
    {
        var apiPermissions = ApiRoutePermissions();
        var seededRoles = SeededRolePermissions();
        var lockouts = new List<string>();

        foreach (var page in CpPages())
        {
            if (page.Permission is null) { continue; }

            // Which non-admin roles are seeded with the page's own gate.
            var holders = RolesHolding(page.Permission, seededRoles);
            if (holders.Count == 0) { continue; }

            var exempt = NotOnLoad.TryGetValue(page.Route, out var skips)
                ? skips
                : Array.Empty<string>();

            foreach (var call in page.Calls)
            {
                if (exempt.Contains(call, StringComparer.Ordinal)) { continue; }

                var required = MatchApiPermission(call, apiPermissions);
                if (required is null) { continue; }        // unmapped, anonymous, or role-gated only
                if (required == page.Permission) { continue; }

                foreach (var role in holders)
                {
                    if (seededRoles[role].Contains(required)) { continue; }

                    lockouts.Add(
                        $"{page.Route} opens for {role} (via {page.Permission}) but calls "
                        + $"{call}, which requires {required}");
                }
            }
        }

        Assert.True(
            lockouts.Count == 0,
            "A role can open these pages but cannot reach an endpoint they call on "
            + "load, so the page renders and its first fetch 403s. Fix the PAGE - "
            + "point it at an endpoint gated on a permission the role holds, or add "
            + "a scoped endpoint for it. Do NOT widen the endpoint's gate (it is "
            + "shared) and do NOT grant the role the extra permission (that widens "
            + "the role far past this one screen). Lockouts: "
            + string.Join("; ", lockouts));
    }

    /// <summary>The allow-list above may only name calls a page really makes.
    ///
    /// <para>Without this, a route that stops making an exempted call - or is
    /// renamed - leaves an entry that silently exempts nothing, and the next
    /// reader trusts a line that no longer describes the code.</para></summary>
    [Fact]
    public void The_not_on_load_allow_list_names_only_calls_that_still_exist()
    {
        var pages = CpPages().ToDictionary(page => page.Route, StringComparer.Ordinal);
        var stale = new List<string>();

        foreach (var (route, calls) in NotOnLoad)
        {
            if (!pages.TryGetValue(route, out var page))
            {
                stale.Add($"{route} (no such page)");
                continue;
            }
            foreach (var call in calls)
            {
                if (!page.Calls.Contains(call, StringComparer.Ordinal))
                {
                    stale.Add($"{route} -> {call} (page no longer calls it)");
                }
            }
        }

        Assert.True(stale.Count == 0,
            "NotOnLoad entries that no longer describe the code: "
            + string.Join("; ", stale));
    }

    private sealed record CpPage(string Route, string? Permission, IReadOnlyList<string> Calls);

    /// <summary>Every CP page: its route, its [RequirePermission] code, and every
    /// /account/api path it references in the .razor or its code-behind.</summary>
    private static List<CpPage> CpPages()
    {
        var codes = PermissionExpressions();
        var pagesDir = Path.Combine(
            RepoRoot,
            "src/ControlPanel/SIMF.ControlPanel/Components/Pages".Replace('/', Path.DirectorySeparatorChar));

        var pages = new List<CpPage>();
        foreach (var file in Directory.EnumerateFiles(pagesDir, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            var route = Regex.Match(markup, @"^@page\s+""([^""]+)""", RegexOptions.Multiline);
            if (!route.Success) { continue; }

            var gate = Regex.Match(markup,
                @"@attribute\s*\[RequirePermission\(\s*(PermissionCatalog\.\w+\.\w+)\s*\)\]");
            string? permission = gate.Success && codes.TryGetValue(gate.Groups[1].Value, out var code)
                ? code
                : null;

            var source = markup;
            var codeBehind = file + ".cs";
            if (File.Exists(codeBehind)) { source += File.ReadAllText(codeBehind); }

            var calls = Regex.Matches(source, @"""(/account/api/[a-zA-Z0-9/_{}$.-]+)")
                .Select(m => m.Groups[1].Value.TrimEnd('/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            pages.Add(new CpPage(route.Groups[1].Value, permission, calls));
        }
        return pages;
    }

    /// <summary>Each API route to the permission its Configure() demands. Routes
    /// with no PolicyFor(...) - anonymous, or gated by role alone - map to null
    /// and are skipped by the caller rather than assumed open.</summary>
    private static Dictionary<string, string?> ApiRoutePermissions()
    {
        var codes = PermissionExpressions();
        var endpointsDir = Path.Combine(
            RepoRoot, "src/Backend/SIMF.Api/Endpoints".Replace('/', Path.DirectorySeparatorChar));

        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(endpointsDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (Match configure in Regex.Matches(
                source, @"public override void Configure\(\)\s*\{(.*?)\n    \}", RegexOptions.Singleline))
            {
                var body = configure.Groups[1].Value;
                var route = Regex.Match(body, @"\b(?:Get|Post|Put|Delete|Patch)\(\s*""([^""]+)""");
                if (!route.Success) { continue; }

                var policy = Regex.Match(body, @"PolicyFor\(\s*(PermissionCatalog\.\w+\.\w+)\s*\)");
                map[Normalise(route.Groups[1].Value)] =
                    policy.Success && codes.TryGetValue(policy.Groups[1].Value, out var code)
                        ? code
                        : null;
            }
        }
        return map;
    }

    /// <summary>The permission an /account/api call ends up demanding. The BFF
    /// forwards /account/api/X to the API's /X, so the suffix is the lookup key
    /// once both sides have their route parameters flattened.</summary>
    private static string? MatchApiPermission(string call, Dictionary<string, string?> api)
    {
        var suffix = call["/account/api".Length..];
        return api.TryGetValue(Normalise(suffix), out var permission) ? permission : null;
    }

    /// <summary>Route parameters and interpolations collapse to {}, so
    /// <c>/admin/sessions/{sessionId:guid}/seat-map</c> and the page's
    /// <c>/admin/sessions/{session.Id}/seat-map</c> compare equal.</summary>
    private static string Normalise(string route) =>
        Regex.Replace(route, @"\{[^}]*\}", "{}").Trim('/');

    /// <summary>Role name to the permission codes PermissionCatalog seeds it.
    /// Administrator is excluded: it holds the "*" wildcard, so it can never be
    /// locked out and including it would make every page look reachable.</summary>
    private static Dictionary<string, HashSet<string>> SeededRolePermissions()
    {
        var byRole = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var definition in PermissionCatalog.All)
        {
            foreach (var role in definition.BaselineRoles)
            {
                if (string.Equals(role, AppRoles.Administrator, StringComparison.Ordinal)) { continue; }
                if (!byRole.TryGetValue(role, out var codes))
                {
                    codes = new HashSet<string>(StringComparer.Ordinal);
                    byRole[role] = codes;
                }
                codes.Add(definition.Code);
            }
        }
        return byRole;
    }

    private static List<string> RolesHolding(
        string permission, Dictionary<string, HashSet<string>> seeded) =>
        seeded.Where(entry => entry.Value.Contains(permission))
            .Select(entry => entry.Key)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList();

    /// <summary>"PermissionCatalog.Sessions.Edit" to "Sessions.Edit", reflected off
    /// the catalogue so the map cannot drift from it.</summary>
    private static Dictionary<string, string> PermissionExpressions()
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
