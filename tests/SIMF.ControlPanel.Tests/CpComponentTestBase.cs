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
    /// the test suite, only the resx-coverage tests need updating).</summary>
    private sealed class PassThroughStringLocalizer : IStringLocalizer<Strings>
    {
        public LocalizedString this[string name] =>
            new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Array.Empty<LocalizedString>();
    }
}
