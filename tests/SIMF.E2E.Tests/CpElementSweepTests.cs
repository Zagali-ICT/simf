// WS4 — the Control Panel element sweep, run unattended.
//
// This is the part of the QA programme that could not be automated before. The
// Chrome DevTools MCP pass drives the same sweep interactively and is the right
// tool for exploration, but it needs an agent in the loop, so nothing checks a
// page between one person deciding to look and the next. That gap is how
// /admin/companies stayed marked "Real" in PAGE-INDEX for eight weeks after the
// page was deleted.
//
// Deliberately NARROW. It does not re-implement the 2858 hand-authored
// scenarios — those would rot. It runs the two GENERATED contracts that every
// page carries (E2E-{NS}-ELS-001/002) plus the auth gate, all of which are
// derived from source and therefore stay true as the CP changes.
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace SIMF.E2E.Tests;

/// <summary>Launches one browser and signs in ONCE for the whole class.
///
/// <para>This is a class fixture, not <c>IAsyncLifetime</c>, and the difference
/// is not an optimisation. xUnit constructs a fresh instance of a test class for
/// every case, so `IAsyncLifetime.InitializeAsync` runs per test — roughly 97
/// sign-ins for this theory. A TOTP code is valid for one 30-second window, and
/// with the cases running in parallel dozens of them submit the same code at
/// once; the server rejects the repeats and the page reports "The verification
/// code is not correct." That reads as a bad credential and is really a broken
/// test design. Signing in once and replaying the storage state fixes the
/// correctness problem and removes 96 logins.</para></summary>
public sealed class CpSessionFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser? Browser { get; private set; }

    public string? StorageState { get; private set; }

    public async Task InitializeAsync()
    {
        if (QaStack.SkipReasonFor(QaStack.ControlPanel) is not null
            || QaStack.CredentialSkipReason() is not null)
        {
            return; // every test will skip; do not pay for a browser
        }
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await CpSignIn.SignInAsync(page);
        StorageState = await context.StorageStateAsync();
        await context.CloseAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) { await Browser.CloseAsync(); }
        _playwright?.Dispose();
    }
}

public sealed class CpElementSweepTests(CpSessionFixture session)
    : IClassFixture<CpSessionFixture>
{
    private readonly CpSessionFixture _session = session;

    /// <summary>Routes whose grids only exist after a parent selection, with the
    /// selection each one needs. Deliberately a hand-maintained list: it is short,
    /// it documents the gap, and it keeps "unverified" from quietly spreading. Each
    /// entry is a per-page E2E scenario waiting to be authored — the generic sweep
    /// cannot drive them because it opens a URL and nothing else.</summary>
    private static readonly Dictionary<string, string> PagesNeedingPrecondition =
        new(StringComparer.Ordinal)
        {
            ["/admin/meeting-tables"] =
                "the tables and allocations grids render only once a hall is chosen "
                + "(@if (_hallId is not null))",
            ["/admin/speaker-presentations"] =
                "the presentations grid replaces the speaker picker only once a "
                + "speaker is chosen (@if (!string.IsNullOrEmpty(_speakerId)))",
        };

    /// <summary>Every Control Panel route the predicted-inventory generator
    /// found, so the list cannot drift from the pages that actually exist.</summary>
    public static TheoryData<string> CpRoutes()
    {
        var data = new TheoryData<string>();
        foreach (var route in PredictedInventory.Routes())
        {
            data.Add(route);
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(CpRoutes))]
    public async Task Page_meets_its_element_contract(string route)
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.ControlPanel);
        Skip.If(stackDown is not null, stackDown);
        var missingCredentials = QaStack.CredentialSkipReason();
        Skip.If(missingCredentials is not null, missingCredentials);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
            StorageState = _session.StorageState,
        });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        await page.GotoAsync(QaStack.ControlPanel + route,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // A permission denial or a session bounce is not a swept page: sweeping
        // whatever we landed on would report a pass for a page we never opened.
        var landed = new Uri(page.Url).AbsolutePath;
        Assert.False(
            landed.StartsWith("/login", StringComparison.OrdinalIgnoreCase),
            $"{route} bounced to {landed} — the sweep account's session expired "
            + "or it lacks the permission this page needs.");

        // E2E-{NS}-ELS-001 + -002.
        var report = await ElementSweep.RunAsync(page);
        Assert.True(report.Pass, report.Describe());
        Assert.True(
            consoleErrors.Count == 0,
            $"{route} logged console errors:\n  " + string.Join("\n  ", consoleErrors));

        // The expected-vs-actual half: a page whose Delete button vanished still
        // sweeps clean, because a control that is not there cannot be unnamed or
        // broken. Only the predicted inventory catches that.
        var predicted = PredictedInventory.For(route);
        if (predicted is { } expected)
        {
            // A master-detail page keeps its grids behind a parent selection, so
            // opening the URL alone renders none of them and every prediction
            // derived from the .razor source is unmeetable. That is NOT a defect,
            // and it is not a pass either — the controls simply were not exercised.
            // Skipping (visibly, with a reason) beats both a false red and a silent
            // green. The route must be named below: any OTHER page that renders no
            // grid while predicting toolbar buttons falls through to a hard failure,
            // so a grid that genuinely regressed away still breaks the build.
            if (expected.ToolbarButtonCount > 0 && report.Counts.Grids == 0)
            {
                PagesNeedingPrecondition.TryGetValue(route, out var precondition);
                Skip.If(
                    precondition is not null,
                    $"{route}: {precondition} — its grid controls need a "
                    + "hand-authored per-page scenario, not the generic sweep.");

                Assert.Fail(
                    $"{route} rendered no SimfDataGrid, but its .razor source wires "
                    + $"{expected.ToolbarButtonCount} toolbar button(s) "
                    + $"({string.Join(", ", expected.ToolbarActions)}). Either the "
                    + "grid regressed away, or this page needs a precondition — in "
                    + "which case add it to PagesNeedingPrecondition with the reason.");
            }

            Assert.True(
                report.Counts.Buttons >= expected.ToolbarButtonCount,
                $"{route} renders {report.Counts.Buttons} buttons but its wired "
                + $"SimfDataGrid callbacks predict at least "
                + $"{expected.ToolbarButtonCount} toolbar buttons "
                + $"({string.Join(", ", expected.ToolbarActions)}). A missing "
                + "action is invisible to a presence-only sweep.");

            // Phase A of the two-phase contract: with nothing selected, every
            // selection-gated action must be present AND disabled. "Absent" and
            // "correctly greyed out" look identical without this.
            if (expected.DisabledAtZeroSelection.Count > 0)
            {
                Assert.True(
                    report.Disabled.Buttons >= expected.DisabledAtZeroSelection.Count,
                    $"{route} has {report.Disabled.Buttons} disabled buttons at "
                    + "zero selection; the wiring predicts at least "
                    + $"{expected.DisabledAtZeroSelection.Count} "
                    + $"({string.Join(", ", expected.DisabledAtZeroSelection)}).");
            }
        }

        await context.CloseAsync();
    }

    /// <summary>E2E-BF-13-012 — `/not-permitted` renders correctly in Arabic.
    ///
    /// <para>This is the page a signed-in admin lands on when their role lacks a
    /// permission, so it is the one page in the Control Panel that a user only
    /// ever sees at a bad moment. It must still be a real, readable, mirrored page
    /// rather than a raw key or a broken layout.</para>
    ///
    /// <para><b>Its sibling, E2E-BF-13-008 (a deep-link bypass attempt is bounced
    /// here), is NOT automated and cannot be from this suite.</b> It needs a
    /// signed-in admin who LACKS a permission, and the sweep account is the
    /// super-admin whose wildcard `"*"` satisfies every gate — so it can never be
    /// redirected and can never exercise the deny path. Creating a restricted
    /// admin does not help either: `SignInService` forces TOTP for any user with a
    /// role (`roles.Count > 0`), and a freshly created admin has no enrolled
    /// authenticator key, so a black-box browser suite cannot complete its
    /// sign-in. It needs a fixture that seeds a restricted admin WITH a known TOTP
    /// secret, which means database access this suite deliberately does not have.
    /// The API half of the same rule is covered — `PermissionEnforcementTests`
    /// proves the endpoint returns 403 — so what remains unproven is specifically
    /// the CP's redirect, not the gate itself.</para></summary>
    [SkippableFact]
    public async Task Not_permitted_renders_in_Arabic()
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.ControlPanel);
        Skip.If(stackDown is not null, stackDown);
        var missingCredentials = QaStack.CredentialSkipReason();
        Skip.If(missingCredentials is not null, missingCredentials);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
            StorageState = _session.StorageState,
            Locale = "ar-SA",
        });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        await page.GotoAsync(
            $"{QaStack.ControlPanel}/culture?culture=ar"
            + $"&redirectUri={Uri.EscapeDataString("/not-permitted")}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        Assert.Equal("/not-permitted", new Uri(page.Url).AbsolutePath);

        var report = await ElementSweep.RunAsync(page);
        Assert.Equal("rtl", report.Dir);
        Assert.True(report.Pass, $"[ar] {report.Describe()}");
        Assert.True(
            consoleErrors.Count == 0,
            "[ar] /not-permitted logged console errors:\n  "
            + string.Join("\n  ", consoleErrors));

        // A localisation miss on THIS page shows the user a resx key at the exact
        // moment they are already confused, so assert real copy, not just a render.
        var heading = await page.TextContentAsync("h1") ?? "";
        Assert.False(
            heading.Contains("NotPermitted.", StringComparison.Ordinal),
            $"/not-permitted rendered a raw resource key as its heading: '{heading}'.");
        Assert.False(
            string.IsNullOrWhiteSpace(heading),
            "/not-permitted rendered an empty heading.");

        await context.CloseAsync();
    }

    /// <summary>The Arabic pass — BF-14's Control Panel half.
    ///
    /// <para>The Website got an RTL sweep with WS2; the CP had none, and it is the
    /// surface with far more layout to break: grids with pinned action columns,
    /// toolbars, pagers, modals. RTL is where a fixed width or an unmirrored
    /// margin shows up, and `scrollWidth > clientWidth` catches exactly that.</para>
    ///
    /// <para>Asserts the sweep contract and a clean console, but NOT the predicted
    /// inventory — the control set does not change with language, so checking it
    /// twice would only double the failure noise from one defect.</para></summary>
    [SkippableTheory]
    [MemberData(nameof(CpRoutes))]
    public async Task Page_meets_its_element_contract_in_Arabic(string route)
    {
        var stackDown = QaStack.SkipReasonFor(QaStack.ControlPanel);
        Skip.If(stackDown is not null, stackDown);
        var missingCredentials = QaStack.CredentialSkipReason();
        Skip.If(missingCredentials is not null, missingCredentials);

        var context = await _session.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = 1440, Height = 900 },
            StorageState = _session.StorageState,
            Locale = "ar-SA",
        });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        // Switch culture the way the CP's own picker does, landing on the route.
        await page.GotoAsync(
            $"{QaStack.ControlPanel}/culture?culture=ar"
            + $"&redirectUri={Uri.EscapeDataString(route)}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var landed = new Uri(page.Url).AbsolutePath;
        Assert.False(
            landed.StartsWith("/login", StringComparison.OrdinalIgnoreCase),
            $"[ar] {route} bounced to {landed}.");

        var report = await ElementSweep.RunAsync(page);
        Assert.Equal("rtl", report.Dir);
        Assert.True(report.Pass, $"[ar] {report.Describe()}");
        Assert.True(
            consoleErrors.Count == 0,
            $"[ar] {route} logged console errors:\n  "
            + string.Join("\n  ", consoleErrors));

        await context.CloseAsync();
    }
}

/// <summary>Reads the committed prediction produced by
/// <c>tools/qa/predicted_inventory.py</c>. Committed rather than regenerated at
/// test time so the runner needs no Python, and so a diff against it after a UI
/// change shows exactly which controls appeared or disappeared.</summary>
public static class PredictedInventory
{
    public sealed record Entry(
        string Route,
        int ToolbarButtonCount,
        IReadOnlyList<string> ToolbarActions,
        IReadOnlyList<string> DisabledAtZeroSelection);

    private static readonly Lazy<Dictionary<string, Entry>> Entries = new(Load);

    /// <summary>Sweepable routes: those that can be opened by URL alone.
    ///
    /// <para>Routes with a path parameter (<c>/sessions/{SessionId:guid}/moderate</c>)
    /// are excluded. Requesting one literally 404s, and the sweep would then
    /// grade the not-found page — reporting a clean pass for a page it never
    /// opened. Giving them a fabricated id is no better: the screen renders its
    /// not-found state and the result is the same lie. They need seeded data,
    /// which belongs in a per-page scenario, not in a generic sweep.</para></summary>
    public static IEnumerable<string> Routes() =>
        Entries.Value.Keys
            .Where(route => !route.Contains('{'))
            // The sign-in pages are excluded from the SIGNED-IN sweep: an
            // authenticated session is redirected away from them, so the sweep
            // would either grade the dashboard while believing it was on /login,
            // or fail for doing the right thing. Their element contract belongs
            // to the signed-out scenarios in cp-auth-flow.md.
            .Where(route => !route.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
            .OrderBy(route => route, StringComparer.Ordinal);

    public static Entry? For(string route) =>
        Entries.Value.TryGetValue(route, out var entry) ? entry : null;

    private static Dictionary<string, Entry> Load()
    {
        var path = Path.Combine(
            RepoRoot(), "docs", "tests", "element-sweeps", "predicted-inventory.json");
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var route = item.GetProperty("route").GetString();
            if (route is null || !route.StartsWith('/'))
            {
                continue;
            }
            // Bespoke pages carry `predicted: null` — they need a hand-authored
            // expectation. They are still swept; only the inventory diff is
            // skipped, and the generator reports them so the gap stays visible.
            if (!item.TryGetProperty("predicted", out var predicted)
                || predicted.ValueKind != JsonValueKind.Object)
            {
                result[route] = new Entry(route, 0, [], []);
                continue;
            }
            result[route] = new Entry(
                route,
                predicted.GetProperty("toolbar_button_count").GetInt32(),
                [.. predicted.GetProperty("toolbar_buttons").EnumerateArray()
                    .Select(b => b.GetProperty("action").GetString() ?? "")],
                [.. predicted.GetProperty("disabled_at_zero_selection").EnumerateArray()
                    .Select(d => d.GetString() ?? "")]);
        }
        return result;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
    }
}
