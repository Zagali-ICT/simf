// E2E-BF-13-008 — the deny path, driven in a real browser at last.
//
// This is the scenario the whole QA programme could not reach. Every browser pass
// signs in as the super-admin, whose permission claim is the wildcard "*"
// (PermissionCatalog.Wildcard); that account satisfies every gate, so it can never
// be redirected to /not-permitted and cannot demonstrate that the gate does
// anything at all. A suite that only ever drives an all-powerful account proves
// the ALLOW half of an access-control system and silently assumes the other half.
//
// `tools/qa/seed-restricted-admin.sql` supplies the missing account: an admin
// granted exactly Sessions.View, sharing the QA super-admin's password hash and
// authenticator-key token so a black-box runner can actually complete its CP
// sign-in (SignInService forces TOTP for anyone holding a role, and a freshly
// created admin has no enrolled key — which is why creating one through the API
// does not work).
//
// The pair of assertions matters more than either alone:
//   * the GRANTED page renders  — the fixture really did sign in, so a "denied"
//     result cannot be a broken login wearing a convincing disguise;
//   * the UNGRANTED page bounces — the gate bites.
// Without the first, this file could pass while proving nothing.
using Microsoft.Playwright;
using Xunit;

namespace SIMF.E2E.Tests;

/// <summary>Signs the RESTRICTED admin in once for the class, mirroring
/// <see cref="CpSessionFixture"/>. Same 30-second-TOTP reasoning: a code is valid
/// for one window, so signing in per test would have parallel cases submitting the
/// same code and reading the rejections as credential failures.</summary>
public sealed class CpRestrictedSessionFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser? Browser { get; private set; }

    public string? StorageState { get; private set; }

    /// <summary>Why the fixture is unusable, or null when it signed in. Carried
    /// rather than thrown so the tests SKIP with a reason on a stack that has not
    /// been seeded, instead of failing for an absent fixture.</summary>
    public string? SetupFailure { get; private set; }

    public async Task InitializeAsync()
    {
        if (QaStack.SkipReasonFor(QaStack.ControlPanel) is not null
            || QaStack.CredentialSkipReason() is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        try
        {
            await CpSignIn.SignInAsync(page, QaStack.RestrictedAdminEmail);
            StorageState = await context.StorageStateAsync();
        }
        catch (Exception ex)
        {
            SetupFailure =
                $"The restricted admin ({QaStack.RestrictedAdminEmail}) could not "
                + "sign in. Seed it with:\n"
                + "  sqlcmd -S \"(localdb)\\MSSQLLocalDB\" -d SIMF_QA_Identity -I "
                + "-i tools/qa/seed-restricted-admin.sql\n"
                + $"Underlying failure: {ex.Message}";
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) { await Browser.CloseAsync(); }
        _playwright?.Dispose();
    }
}

public sealed class CpPermissionDenyPathTests(CpRestrictedSessionFixture session)
    : IClassFixture<CpRestrictedSessionFixture>
{
    private readonly CpRestrictedSessionFixture _session = session;

    /// <summary>The control. If this fails, every "denied" result in this file is
    /// worthless — a sign-in that never happened denies everything too.</summary>
    [SkippableFact]
    public async Task The_restricted_admin_reaches_the_page_it_IS_granted()
    {
        var page = await OpenAsync(QaStack.RestrictedAdminGrantedRoute);

        Assert.Equal(
            QaStack.RestrictedAdminGrantedRoute,
            new Uri(page.Url).AbsolutePath);
        Assert.DoesNotContain("/not-permitted", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>E2E-BF-13-008 proper — typing an ungranted route into the address
    /// bar is bounced to <c>/not-permitted</c>. This is the assertion the sweep
    /// account structurally could not make.</summary>
    [SkippableFact]
    public async Task A_deep_link_to_an_ungranted_page_is_bounced_to_not_permitted()
    {
        var page = await OpenAsync(QaStack.RestrictedAdminDeniedRoute);

        var landed = new Uri(page.Url).AbsolutePath;
        Assert.True(
            landed.Equals("/not-permitted", StringComparison.OrdinalIgnoreCase),
            $"A deep link to {QaStack.RestrictedAdminDeniedRoute} by an admin "
            + $"WITHOUT that permission landed on '{landed}' instead of "
            + "/not-permitted. If it rendered the page, the gate is missing and "
            + "any signed-in admin can reach it regardless of role.");
    }

    /// <summary>The bounce must not be a dead end. An admin who lands here should
    /// see real copy and a way back, not a blank page or a raw resource key.</summary>
    [SkippableFact]
    public async Task The_not_permitted_page_is_usable_when_reached_by_denial()
    {
        var page = await OpenAsync(QaStack.RestrictedAdminDeniedRoute);

        var report = await ElementSweep.RunAsync(page);
        Assert.True(report.Pass, report.Describe());

        var heading = await page.TextContentAsync("h1") ?? "";
        Assert.False(
            string.IsNullOrWhiteSpace(heading),
            "/not-permitted rendered an empty heading.");
        Assert.False(
            heading.Contains("NotPermitted.", StringComparison.Ordinal),
            $"/not-permitted rendered a raw resource key: '{heading}'.");
    }

    /// <summary>The nav must not advertise what the account cannot open. A link to
    /// a page that bounces is a dead control by any reasonable definition, and
    /// `CpNavigation.RequiredPermission` exists precisely to prevent it.</summary>
    [SkippableFact]
    public async Task The_side_nav_does_not_offer_a_page_the_account_cannot_open()
    {
        var page = await OpenAsync(QaStack.RestrictedAdminGrantedRoute);

        var hrefs = await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('nav a[href]')]"
            + ".map(a => new URL(a.getAttribute('href'), document.baseURI).pathname)");

        Assert.DoesNotContain(
            QaStack.RestrictedAdminDeniedRoute,
            hrefs,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IPage> OpenAsync(string route)
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.ControlPanel);
        Skip.If(stackDown is not null, stackDown);
        var missingCredentials = QaStack.CredentialSkipReason();
        Skip.If(missingCredentials is not null, missingCredentials);
        Skip.If(_session.SetupFailure is not null, _session.SetupFailure);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
            StorageState = _session.StorageState,
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(QaStack.ControlPanel + route,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // A bounce to /login means the fixture's session died, not that the page
        // is gated — reporting that as a permission denial would be a lie.
        Assert.DoesNotContain("/login", new Uri(page.Url).AbsolutePath,
            StringComparison.OrdinalIgnoreCase);
        return page;
    }
}
