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
using System.Net;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Web;
using SIMF.Web.Content;

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

        // D-755 — the public marketing pages resolve the forum date via ForumDates
        // (backed by SimfPublicClient + IMemoryCache). Registered here so any page
        // that injects it can render; a test supplies the SimfPublicClient it needs.
        Services.AddMemoryCache();
        Services.AddScoped<ForumDates>();

        // The shared LandingHeader (rendered on every marketing page via LandingShell)
        // now resolves the Archive editions dropdown via PublicEditions, so it must be
        // resolvable in every page test. A default unavailable SimfPublicClient is
        // registered so both providers fall back gracefully; a test that needs real
        // data registers its own SimfPublicClient afterwards (last registration wins).
        Services.AddSingleton(new SimfPublicClient(new HttpClient(new UnavailableHandler())
        {
            BaseAddress = new Uri("https://api.test/"),
        }));
        Services.AddScoped<PublicEditions>();

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

    // The default public client for pages that do not stub their own: every request
    // returns 503, so GetArchiveAsync / GetOrganizationProfileAsync yield null and the
    // providers use their static fallbacks.
    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

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
