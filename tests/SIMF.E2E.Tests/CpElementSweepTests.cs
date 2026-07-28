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

public sealed class CpElementSweepTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        if (QaStack.SkipReasonFor(QaStack.ControlPanel) is not null)
        {
            return; // every test will skip; do not pay for a browser
        }
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) { await _browser.CloseAsync(); }
        _playwright?.Dispose();
    }

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

        var context = await _browser!.NewContextAsync(
            new BrowserNewContextOptions { ViewportSize = new() { Width = 1440, Height = 900 } });
        var page = await context.NewPageAsync();

        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") { consoleErrors.Add(message.Text); }
        };

        await CpSignIn.SignInAsync(page);
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

    public static IEnumerable<string> Routes() => Entries.Value.Keys.OrderBy(r => r, StringComparer.Ordinal);

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
