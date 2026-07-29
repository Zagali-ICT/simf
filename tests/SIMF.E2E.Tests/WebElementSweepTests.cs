// WS2 — the Website surface, swept unattended.
//
// The public site is the easier half of the sweep and the one with the most to
// lose: it is the only surface an unauthenticated stranger sees, so a broken
// image or a dead link there is visible to everyone rather than to the handful of
// admins who use the Control Panel.
//
// Simpler than the CP suite in one important way — no sign-in, so no shared
// session, no TOTP, and every route is independent. It is also weaker in one way:
// there is no predicted inventory. `tools/qa/predicted_inventory.py` derives its
// expectations from SimfDataGrid wiring, and the Website has no grids — its pages
// are bespoke SSR content. So this asserts the sweep's own contract (every control
// accessibly named, no broken image, every same-origin link and asset < 400, no
// horizontal overflow) plus a clean console, and nothing about which controls
// SHOULD exist. That gap is real and is what the per-page web-*.md catalogues are
// for; it is stated here rather than papered over.
using Microsoft.Playwright;
using Xunit;

namespace SIMF.E2E.Tests;

/// <summary>One browser for the whole class. No sign-in — the site is public.</summary>
public sealed class WebSessionFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        if (QaStack.SkipReasonFor(QaStack.Website) is not null)
        {
            return; // every test will skip; do not pay for a browser
        }
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) { await Browser.CloseAsync(); }
        _playwright?.Dispose();
    }
}

public sealed class WebElementSweepTests(WebSessionFixture session)
    : IClassFixture<WebSessionFixture>
{
    private readonly WebSessionFixture _session = session;

    /// <summary>Every parameterless Website route, taken from the `@page`
    /// directives in <c>src/Website/SIMF.Web/Components/Pages</c>.
    ///
    /// <para><c>/sessions/{Id:guid}</c> is excluded for the same reason the CP
    /// suite excludes its parameterised routes: requesting it literally renders
    /// the not-found state, and the sweep would then grade a page it never opened.
    /// It needs seeded data, which belongs in a per-page scenario.</para></summary>
    public static TheoryData<string> WebRoutes()
    {
        var data = new TheoryData<string>();
        foreach (var route in new[]
        {
            "/",
            "/about",
            "/about/objectives",
            "/about/organizer",
            "/about/themes",
            "/about/venue",
            "/archive",
            "/discover",
            "/landing",
            "/partners",
            "/programme",
            "/programme/exhibition",
            "/programme/gov-meetings",
            "/programme/opening",
            "/programme/sessions",
            "/speakers",
            "/visit",
        })
        {
            data.Add(route);
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(WebRoutes))]
    public async Task Page_meets_its_element_contract(string route)
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.Website);
        Skip.If(stackDown is not null, stackDown);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
        });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        var response = await page.GotoAsync(QaStack.Website + route,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // A public page that does not return 200 is a defect on its own, and
        // sweeping the error page it served instead would report a false pass.
        Assert.True(
            response is not null && response.Status < 400,
            $"{route} returned {response?.Status.ToString() ?? "no response"}.");

        var report = await ElementSweep.RunAsync(page);
        Assert.True(report.Pass, report.Describe());
        Assert.True(
            consoleErrors.Count == 0,
            $"{route} logged console errors:\n  " + string.Join("\n  ", consoleErrors));

        await context.CloseAsync();
    }

    /// <summary>The Arabic pass. The Website is bilingual and RTL is where layout
    /// breaks — a fixed width, an unmirrored icon, a long Arabic string forcing the
    /// page wider than the viewport. Running the same contract under `?culture=ar`
    /// costs one extra navigation per route and is the only automated RTL check the
    /// public site has.</summary>
    [SkippableTheory]
    [MemberData(nameof(WebRoutes))]
    public async Task Page_meets_its_element_contract_in_Arabic(string route)
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.Website);
        Skip.If(stackDown is not null, stackDown);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
            Locale = "ar-SA",
        });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        // Switch culture the way the site's own picker does, then land on the page.
        await page.GotoAsync(
            $"{QaStack.Website}/culture?culture=ar&redirectUri={Uri.EscapeDataString(route)}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var report = await ElementSweep.RunAsync(page);
        Assert.True(report.Pass, $"[ar] {report.Describe()}");
        Assert.True(
            consoleErrors.Count == 0,
            $"[ar] {route} logged console errors:\n  "
            + string.Join("\n  ", consoleErrors));

        await context.CloseAsync();
    }
}
