// D-191 (project-wide deferred gap closure since D-177) — bUnit test
// base for CP Razor components. Wires the minimum service set every
// CP page needs:
//
//   • IStringLocalizer<Strings> — returns each key as-is so assertions
//     can match against the resx key (no resx lookup at test time).
//   • IJSRuntime — supplied by bUnit's built-in JSInterop mock. Tests
//     stub the simfAccount.{postJson,getJson,putJson} calls per-test.
//   • NavigationManager — bUnit ships a fake; URI defaults to localhost.
//   • Authorization — registered as Administrator role so [Authorize]-
//     attributed pages render.
//
// Use as the base for any bUnit test class:
//
//   public sealed class MyPageTests : CpComponentTestBase
//   {
//       [Fact]
//       public void Renders_initial_state()
//       {
//           var cut = RenderComponent<MyPage>();
//           cut.Find("[data-testid='title']").MarkupMatches("<h1>...</h1>");
//       }
//   }
//
// The base disposes the bUnit TestContext after each test so the
// JSInterop mock state, registered services, and the test-DOM are all
// per-test (xUnit fact-isolation).
using System.Text.RegularExpressions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SIMF.ControlPanel;

namespace SIMF.ControlPanel.Tests;

public abstract class CpComponentTestBase : TestContext
{
    /// <summary>The bUnit authorization context — pre-set as an
    /// authenticated Administrator so CP pages with
    /// <c>[Authorize(Roles="Administrator")]</c> render. Subclasses
    /// can call <c>Authorization.SetAuthorized("other-user")</c> /
    /// <c>SetRoles(...)</c> per-test when they need a different
    /// identity.</summary>
    protected TestAuthorizationContext Authorization { get; }

    protected CpComponentTestBase()
    {
        // D-191 — localizer mock returns the key as-is. CP pages
        // bind localized text into the DOM as `@L["Some.Key"]`;
        // tests can assert against the key text directly without
        // depending on the resx values (which would couple the
        // tests to the EN strings).
        Services.AddSingleton<IStringLocalizer<Strings>>(new PassThroughStringLocalizer());

        // Every CP page may inject CpPreferences (the per-page popup/full-page
        // choice, read from JS localStorage). Backed by the bUnit JS mock, so in
        // Loose mode the read returns the Dialog default; a test can re-register
        // its own instance to override.
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));

        // D-191 — register the bUnit test auth context with the
        // Administrator role pre-set so [Authorize]-attributed pages
        // render. Pages that need a different role can override
        // per-test via the Authorization property.
        Authorization = this.AddTestAuthorization();
        Authorization.SetAuthorized("test-admin@simf.test");
        Authorization.SetRoles("Administrator");
    }

    /// <summary>D-191 — IStringLocalizer mock that returns the key
    /// as-is for every lookup. Lets tests assert against resx keys
    /// rather than EN strings (so a future resx rename doesn't break
    /// the test suite, only the resx-coverage tests need updating).
    ///
    /// <para>§6.16 (LOC-010) — it used to report <c>resourceNotFound: false</c>
    /// for EVERY lookup, including keys that exist in no resx at all. That is
    /// what made the whole CP suite structurally blind to a missing key: a page
    /// referencing <c>Admin.Sessions.Field.Start</c> when the resx only had
    /// <c>...StartUtc</c> rendered the raw key on screen in production, and
    /// every bUnit test of that page still passed. Four such defects
    /// (LOC-001/002/003/004) shipped that way and were only caught by driving
    /// the pages in a browser.</para>
    ///
    /// <para>The pass-through VALUE is kept — that design is sound and the
    /// assertions depend on it. What changes is honesty: a key absent from
    /// Strings.resx now throws, so the test that renders the page fails and
    /// names the key.</para></summary>
    private sealed class PassThroughStringLocalizer : IStringLocalizer<Strings>
    {
        public LocalizedString this[string name] =>
            new(name, Verified(name), resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(Verified(name), arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();

        private static string Verified(string name)
        {
            if (!ResxKeys.Value.Contains(name))
            {
                throw new KeyNotFoundException(
                    $"§6.16 (LOC-010): the component looked up the localization key "
                    + $"'{name}', which does not exist in Strings.resx. In production "
                    + "IStringLocalizer returns the KEY on a miss, so this renders the "
                    + "raw developer string on screen. Add the key to Strings.resx and "
                    + "Strings.ar.resx, or fix the lookup.");
            }
            return name;
        }
    }

    /// <summary>Every <c>name</c> declared in the CP's English resx. Read once
    /// per test run.</summary>
    private static readonly Lazy<HashSet<string>> ResxKeys = new(() =>
    {
        var path = Path.Combine(FindRepoRoot(),
            "src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx"
                .Replace('/', Path.DirectorySeparatorChar));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), @"<data name=""([^""]+)"""))
        {
            keys.Add(match.Groups[1].Value);
        }
        return keys;
    });

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory)
            : dir.FullName;
    }
}
