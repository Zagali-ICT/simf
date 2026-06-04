// D-194 — bUnit test base for the Website Razor pages. Mirrors
// SIMF.ControlPanel.Tests.CpComponentTestBase (D-191): the same
// PassThroughStringLocalizer + TestAuthorizationContext shape, but
// for SIMF.Web.Strings and the Website's claim-driven account pages.
//
// Unlike the CP base (which pre-authorises an Administrator), the
// Website pages are claim-driven state banners — each test sets the
// specific claims (account_state, email, rejection_reason, …) it
// needs via the Authorization property's SetClaims, because that's
// exactly the input the page logic branches on.
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SIMF.Web;

namespace SIMF.Web.Tests;

public abstract class WebComponentTestBase : TestContext
{
    /// <summary>The bUnit authorization context. Tests call
    /// <c>Authorization.SetAuthorized("...")</c> + <c>SetClaims(...)</c>
    /// to drive the account-state pages.</summary>
    protected TestAuthorizationContext Authorization { get; }

    protected WebComponentTestBase()
    {
        // D-194 — localizer mock returns each key as-is so tests assert
        // against resx keys, not EN strings (resilient to copy edits).
        Services.AddSingleton<IStringLocalizer<Strings>>(new PassThroughStringLocalizer());

        // Authenticated by default (the account pages carry [Authorize]);
        // each test layers the specific claims it needs on top.
        Authorization = this.AddTestAuthorization();
        Authorization.SetAuthorized("visitor@simf.test");
    }

    /// <summary>D-194 — the bUnit FakeNavigationManager records every
    /// NavigateTo; tests assert on its <c>Uri</c> to verify the
    /// state-banner redirect routing.</summary>
    protected FakeNavigationManager Navigation =>
        Services.GetRequiredService<FakeNavigationManager>();

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
