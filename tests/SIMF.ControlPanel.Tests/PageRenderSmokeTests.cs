using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.ControlPanel;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// D-831 — every routable Control Panel page renders without throwing.
///
/// <para><b>Why this exists.</b> D-830 shipped a Razor comment inside a component
/// tag on <c>MeetingTablesList</c>. Razor reads that as an attribute NAME, so the
/// page threw <c>InvalidOperationException</c>. It compiled, and it passed a clean
/// Release build, a 456-test suite, a five-lens adversarial review and a four-agent
/// simplify pass, because every one of those gates reads source and no test had
/// ever rendered that page.</para>
///
/// <para><b>What it does NOT do, measured rather than assumed.</b> Putting that
/// exact defect back was tried against this suite, and this suite stayed green: the
/// broken tag sits inside <c>@if (_hallId is not null)</c>, so nothing instantiates
/// it until an admin picks a hall. The markup ratchet in
/// <c>ActionPermissionGuardRatchetTests</c> is what catches that one. The two guards
/// are complements, not one subsuming the other — this covers the INITIAL render of
/// every page, the ratchet covers a shape that compiles anywhere in the file.</para>
///
/// <para>What it does catch is still the cheapest thing available: unknown
/// parameters and missing services on first paint, null-refs in OnInitialized, a
/// missing cascading value, and a mistyped localization key (the base's localizer
/// throws on a key absent from the resx). It asserts nothing about behaviour — the
/// per-page suites do that. It only asserts the page exists.</para>
///
/// <para>It found seven pages on its first run that no test had ever rendered: the
/// whole authentication surface plus the account profile. All seven turned out to be
/// missing service registrations in the harness rather than page defects, which is
/// itself the point — nothing had been rendering them to notice.</para>
///
/// <para>The identity is an Administrator holding the wildcard, so permission gates
/// are wide open here by design: this is about rendering, and the gates have their
/// own tests next door.</para>
/// </summary>
public sealed class PageRenderSmokeTests : CpComponentTestBase
{
    /// <summary>Pages that cannot be rendered bare, with the reason. Keep this list
    /// short and each entry specific — an entry is a page with no render coverage at
    /// all, which is exactly the hole this class exists to close.</summary>
    private static readonly Dictionary<string, string> CannotRenderBare = new(StringComparer.Ordinal);

    public PageRenderSmokeTests()
    {
        // Program.cs registers these; CpComponentTestBase does not, because no
        // per-page suite had ever rendered a page that injects them. The sign-in,
        // 2FA, recovery-code and password-reset pages all do — the whole
        // authentication surface, and the part with the least render coverage.
        // Register them here rather than in the base so no existing test's service
        // graph changes underneath it.
        Services.AddSingleton(new SimfAuthClient(
            new HttpClient { BaseAddress = new Uri("https://api.test.local") }));
        Services.AddScoped<SimfAuthSession>();
        Services.AddScoped<SimfUserChrome>();
        Services.AddMemoryCache();
        Services.AddSingleton<SignInTicketStore>();
    }

    public static TheoryData<string> RoutablePages()
    {
        var data = new TheoryData<string>();
        foreach (var page in Pages())
        {
            data.Add(page.FullName!);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RoutablePages))]
    public void The_page_renders(string typeName)
    {
        if (CannotRenderBare.ContainsKey(typeName)) { return; }

        var type = Pages().Single(candidate => candidate.FullName == typeName);

        // Wildcard: this test is about rendering, not about who may press what.
        Authorization.SetPolicies(
            PermissionCatalog.All.Select(p => PermissionCatalog.PolicyFor(p.Code)).ToArray());
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The page type is only known at run time, so build the fragment by hand
        // rather than going through the generic RenderComponent<T>.
        var exception = Record.Exception(() => Render(builder =>
        {
            builder.OpenComponent(0, type);
            builder.CloseComponent();
        }));

        Assert.True(
            exception is null,
            $"{typeName} threw while rendering, so the page is broken for every admin "
            + $"who opens it:\n{exception}");
    }

    [Fact]
    public void The_cannot_render_list_has_no_stale_entries()
    {
        var known = Pages().Select(page => page.FullName!).ToHashSet(StringComparer.Ordinal);
        var stale = CannotRenderBare.Keys.Where(name => !known.Contains(name)).ToList();

        Assert.True(
            stale.Count == 0,
            "Entries name pages that no longer exist:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Every component in the Control Panel carrying a <c>@page</c> route.</summary>
    private static IEnumerable<Type> Pages() =>
        typeof(CpNavigation).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.GetCustomAttributes<RouteAttribute>().Any())
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
}
