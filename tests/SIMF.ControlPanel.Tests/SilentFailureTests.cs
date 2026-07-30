// §6.16 (F-U5-002 / F-U5-003) — the Control Panel used to hide API failures from
// the admin in two distinct ways. Both are ratcheted here because both are easy to
// reintroduce and neither is visible in a passing render.
//
//   F-U5-002 — five list pages substituted an EMPTY page for a failed envelope, so
//              a 500 or a 403 was indistinguishable from "no rows": /admin/admins
//              said "No accounts yet." while the API was actually refusing it.
//   F-U5-003 — SessionsAddEdit swallowed all four lookup loads with `catch { }`,
//              leaving the Hall / Speaker / Theme / Category pickers silently empty.
//              HandleSubmitAsync then hard-fails on the missing hall, so the form
//              could not be saved and nothing on screen explained why.
//
// A note on why BOTH the throw and the returned-envelope paths matter:
// simfReadEnvelope (wwwroot/js/simf-account.js) deliberately converts an HTTP
// failure — and a non-JSON error page — into a RETURNED ApiResult.Fail rather than
// throwing. A try/catch alone therefore never sees the common failure.
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using SIMF.ControlPanel;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SilentFailureTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string CpProjectDir = "src/ControlPanel/SIMF.ControlPanel";

    /// <summary>A catch whose body is COMPLETELY empty — not even a comment.</summary>
    private static readonly Regex UndocumentedEmptyCatch =
        new(@"catch\s*(?:\([^)]*\))?\s*\{\s*\}", RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void No_control_panel_source_swallows_an_exception_without_saying_why()
    {
        // Deliberately NOT comment-stripped. The CP's convention is that a
        // best-effort catch carries a comment explaining the tradeoff — e.g.
        // SessionTimeoutGuard's `catch (JSException) { /* the cookie's own expiry
        // still bounds the session */ }`. Those are decisions, and there are ~25 of
        // them; stripping comments first would flag every one as a defect.
        // What shipped in SessionsAddEdit was different: `catch { }` with nothing
        // inside at all, so nobody could tell whether the swallow was intended.
        var offenders = new List<string>();

        foreach (var file in CpSources())
        {
            var source = File.ReadAllText(file);
            foreach (Match match in UndocumentedEmptyCatch.Matches(source))
            {
                // Skip an occurrence that is itself inside a // comment (several of
                // the §6.16 fix comments quote the pattern they replaced).
                var lineStart = source.LastIndexOf('\n', match.Index) + 1;
                var beforeOnLine = source[lineStart..match.Index];
                if (beforeOnLine.Contains("//", StringComparison.Ordinal)) continue;

                offenders.Add($"{Relative(file)}:~{source.Take(match.Index).Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(offenders.Count == 0,
            "§6.16 (F-U5-003): this catch swallows an exception with an entirely empty "
            + "body. Either report the failure into the page's error surface, or keep "
            + "the catch and write a comment saying why it is safe: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void A_page_toast_is_rendered_above_any_dialog_it_can_fire_behind()
    {
        // §6.16 (F-U5-001) — .simf-modal is position:fixed / z-index:100 with a 45%
        // scrim, and body.simf-modal-open locks scroll. A page-level <SimfAlert>
        // rendered in flow therefore sits BEHIND the dialog and cannot be scrolled
        // to. Pages closed their dialog only on success, so a failed in-dialog
        // action left the dialog open and wrote its reason somewhere invisible: the
        // admin clicked Deactivate, the spinner stopped, and nothing happened.
        // .simf-toast is the existing fix — position:fixed, z-index:110.
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, (CpProjectDir + "/Components").Replace('/', Path.DirectorySeparatorChar)),
            "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("_toast is not null", StringComparison.Ordinal)) continue;

            var hasDialog = source.Contains("<SimfModal", StringComparison.Ordinal)
                         || source.Contains("<CrudShell", StringComparison.Ordinal);
            if (!hasDialog) continue;   // no dialog on this page: in-flow is fine

            if (!source.Contains("class=\"simf-toast\"", StringComparison.Ordinal))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.True(offenders.Count == 0,
            "§6.16 (F-U5-001): these pages host a dialog but render their toast in "
            + "flow, so a failure reported while the dialog is open is painted behind "
            + "the scrim and never seen. Wrap it in <div class=\"simf-toast\">: "
            + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("Components/Pages/Admin/UsersList.razor.cs")]
    [InlineData("Components/Pages/Admin/VisitorsList.razor.cs")]
    [InlineData("Components/Pages/Admin/OthersList.razor.cs")]
    [InlineData("Components/Pages/Admin/ProfileTypes/OtherProfileTypesList.razor.cs")]
    [InlineData("Components/Pages/Admin/ProfileTypes/VisitorProfileTypesList.razor.cs")]
    public void A_failed_list_load_is_reported_not_rendered_as_an_empty_grid(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/" + relativePath).Replace('/', Path.DirectorySeparatorChar)));

        var load = Between(source, "private async Task LoadAsync()", "\r\n    }");

        Assert.True(load.Contains("ShowToast", StringComparison.Ordinal),
            $"§6.16 (F-U5-002): {relativePath} still substitutes an empty page for a "
            + "failed envelope without telling the admin, so an API failure reads as "
            + "\"no rows yet\".");
    }

    [Fact]
    public void Session_form_lookups_report_a_failure_on_both_the_throw_and_the_envelope_path()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/Admin/SessionsAddEdit.razor.cs")
                .Replace('/', Path.DirectorySeparatorChar)));

        // 4 loaders x (catch path + failed-envelope path).
        var reports = Regex.Matches(source, @"ReportLookupFailure\(\);").Count;

        Assert.True(reports >= 8,
            "§6.16 (F-U5-003): each of the four lookup loaders must report a failure "
            + "on BOTH the throw path and the returned-failed-envelope path — "
            + $"found only {reports} of the expected 8 report sites.");
    }

    [Fact]
    public void No_page_starts_an_excel_export_without_handling_the_failure()
    {
        // §6.16 (F-U5-005) — simfAccount.downloadXlsx used to `return` silently on
        // any non-OK status, and every call site used InvokeVoidAsync, so the
        // return value could not have been inspected even if there had been one.
        // The admin clicked Export, no file arrived, and nothing on the page
        // changed. CpExport.ExportXlsxAsync is now the only supported entry point;
        // it returns the localized message to toast, or null on success.
        var offenders = CpSources()
            .Where(f => File.ReadAllText(f)
                .Contains("InvokeVoidAsync(\"simfAccount.downloadXlsx\"", StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "§6.16 (F-U5-005): these call downloadXlsx through InvokeVoidAsync, which "
            + "discards the failure envelope and makes a failed Export look like a dead "
            + "button. Use JS.ExportXlsxAsync(url, request, L) and toast the result: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void The_download_helper_reports_a_failure_instead_of_returning_silently()
    {
        // The C#-side ratchet above is only meaningful while the JS helper actually
        // produces a failure envelope to inspect.
        var js = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/wwwroot/js/simf-account.js")
                .Replace('/', Path.DirectorySeparatorChar)));

        var download = Between(js, "async downloadXlsx(url, body)", "\n    },");

        Assert.DoesNotContain("if (!response.ok) return;", download, StringComparison.Ordinal);
        Assert.Contains("EXPORT_FAILED", download, StringComparison.Ordinal);
    }

    [Fact]
    public void The_excel_import_blocks_the_page_while_it_uploads()
    {
        // §6.16 (F-U5-008) — a spreadsheet import is neither instant nor idempotent.
        // With no busy state the page looked frozen and the Import button stayed
        // live, so a second click imported the same file again and doubled every
        // created row.
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/CrudGridExcel.razor.cs")
                .Replace('/', Path.DirectorySeparatorChar)));
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/CrudGridExcel.razor")
                .Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("if (_importing) return;", source, StringComparison.Ordinal);
        Assert.Contains("_importing = true;", source, StringComparison.Ordinal);
        Assert.Contains("@if (_importing)", markup, StringComparison.Ordinal);
        // A blocking overlay with no OnClose must not render a close button that
        // does nothing — that is the very defect class this sweep is closing.
        Assert.Contains("HideClose=\"true\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_log_viewer_loads_report_their_own_failure()
    {
        // §6.16 (F-U5-009) — the log viewer had two distinct silent failures: a
        // failed LIST fell into the same branch as an empty one, so "I could not
        // reach the log service" rendered as "there are no log files"; and a failed
        // TAIL assigned null, blanking the pane. With auto-refresh on a 5-second
        // poll that wiped the text mid-read. Each needs its OWN message — they are
        // different facts on an incident desk.
        //
        // Asserted on the resource KEYS, not on field names: a key only appears
        // here if the branch that reports it exists.
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/Admin/LogsViewer.razor.cs")
                .Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("Admin.Logs.LoadFailed", source, StringComparison.Ordinal);
        Assert.Contains("Admin.Logs.TailFailed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_media_library_manage_button_reports_a_failed_detail_fetch()
    {
        // §6.16 (F-U5-011) — a failed detail fetch did literally nothing: no modal,
        // no message, no spinner. The Manage button was indistinguishable from an
        // unwired one.
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/Admin/MediaLibraryList.razor.cs")
                .Replace('/', Path.DirectorySeparatorChar)));

        var details = Between(source, "private async Task OnDetailsAsync", "\r\n    }");

        Assert.Contains("_toast = new Toast(\"error\"", details, StringComparison.Ordinal);
    }

    [Fact]
    public void The_registration_gate_page_separates_loading_from_failed()
    {
        // §6.16 (F-U5-007) — "Loading…" keyed off `_gate is null`, which is also
        // true after a FAILED load, so the page read "Loading…" indefinitely.
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/Admin/OperationsToggles.razor")
                .Replace('/', Path.DirectorySeparatorChar)));

        Assert.DoesNotContain("@if (_gate is null)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@if (_archive is null)", markup, StringComparison.Ordinal);
        Assert.Contains("@if (_loading)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Bulk_dismissing_notifications_counts_what_actually_succeeded()
    {
        // §6.16 (F-U5-006) — the loop discarded every per-row result and reported
        // "Dismissed N notifications." unconditionally, so an admin whose N deletes
        // ALL failed was told they had all succeeded.
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/Account/Notifications.razor.cs")
                .Replace('/', Path.DirectorySeparatorChar)));

        var bulk = Between(source, "private async Task OnBulkDeleteAsync", "\r\n    }");

        Assert.Contains("dismissed++", bulk, StringComparison.Ordinal);
        Assert.Contains("BulkDismissedPartial", bulk, StringComparison.Ordinal);
        Assert.Contains("dismissed == 0", bulk, StringComparison.Ordinal);
    }

    [Fact]
    public void The_dark_theme_gives_the_reserved_seat_its_own_colour()
    {
        // §6.16 (DT-003) — the seat palette is the mobile app's, picked against a
        // LIGHT board. --color-seat-free aliases --color-surface-sunken, so in dark
        // theme the reserved seat (#01132D) sat at 1.10:1 against the board and,
        // because the seat sets its border to the same colour, rendered as a hole
        // in the grid while the free seat kept a visible border.
        var tokens = File.ReadAllText(Path.Combine(
            RepoRoot, "src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css"
                .Replace('/', Path.DirectorySeparatorChar)));

        var dark = Between(tokens, "[data-theme=\"dark\"] {", "\n}");

        Assert.Contains("--color-seat-admin:", dark, StringComparison.Ordinal);
        Assert.Contains("--color-seat-random:", dark, StringComparison.Ordinal);
        // Random has to go light enough that a white label stops reading on it.
        Assert.Contains("--color-seat-random-contrast:", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void The_test_localizer_refuses_a_key_that_exists_in_no_resx()
    {
        // §6.16 (LOC-010) — the bUnit localizer reported resourceNotFound:false for
        // EVERY lookup, so a page referencing a key present in no resx rendered the
        // raw key on screen in production while every test of that page passed.
        // Four such defects shipped that way (LOC-001/002/003/004) and were only
        // caught by driving the pages in a browser.
        //
        // Verified through the real DI registration, not a hand-built instance —
        // otherwise this would pass while the base class handed pages something
        // else entirely.
        using var ctx = new LocalizerProbe();
        var localizer = ctx.Localizer;

        // A key that IS in Strings.resx passes through as its own name.
        Assert.Equal("Common.Cancel", localizer["Common.Cancel"].Value);

        var thrown = Assert.Throws<KeyNotFoundException>(
            () => _ = localizer["Admin.ThisKey.DoesNotExist"].Value);
        Assert.Contains("Admin.ThisKey.DoesNotExist", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>Reaches the localizer the CP test base actually registers.</summary>
    private sealed class LocalizerProbe : CpComponentTestBase
    {
        public IStringLocalizer<Strings> Localizer =>
            Services.GetRequiredService<IStringLocalizer<Strings>>();
    }

    [Fact]
    public void The_module_catch_all_only_answers_for_modules_that_exist()
    {
        // §6.16 (NAV-011) — "/m/{Module}" is a catch-all and it rendered the
        // "Coming soon" panel for ANY value, so /m/typo produced a confident,
        // correctly-shelled page announcing a module that does not exist and is
        // not coming. That is worse than a 404: the admin waits for it.
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, (CpProjectDir + "/Components/Pages/ModulePlaceholder.razor")
                .Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("@if (IsKnownModule)", markup, StringComparison.Ordinal);
        Assert.Contains("NotFound.Text", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_statistics_cards_are_declared_once()
    {
        // §6.16 (NAV-008) — the eleven <SimfStatCard> were duplicated verbatim
        // between "/" and "/admin/statistics", so a metric added or relabelled in
        // one would silently disagree with the other.
        //
        // Scoped to the STATISTICS set specifically. Seven other dashboards (AI,
        // Attendance, Gates ops, Programme timeline, Ratings, Services monitor)
        // legitimately declare their own distinct stat cards; a bare count of
        // <SimfStatCard> per file flags all of them and means nothing.
        var offenders = Directory.EnumerateFiles(
                Path.Combine(RepoRoot, (CpProjectDir + "/Components").Replace('/', Path.DirectorySeparatorChar)),
                "*.razor", SearchOption.AllDirectories)
            .Where(f => Regex.Matches(File.ReadAllText(f), @"Admin\.Statistics\.Stat\.").Count > 1)
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 1,
            "§6.16 (NAV-008): the statistics stat-card set must be declared in "
            + "exactly one file (StatisticsCards.razor) so the Dashboard and the "
            + "Statistics page cannot drift. Found: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_form_control_is_named_only_by_a_span()
    {
        // §6.16 (LQ-006 / LQ-008) — a <span class="simf-field__label"> is not a
        // label: it names nothing. Most CP controls are WRAPPED in a <label>,
        // which names them implicitly; the offenders sat in a <div> with a bare
        // span, so they reached assistive tech with no accessible name at all.
        // A <fieldset> has the same problem in its own form — it is named by its
        // <legend>, and a span there leaves the whole group anonymous.
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, (CpProjectDir + "/Components").Replace('/', Path.DirectorySeparatorChar)),
            "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            // A <div class="simf-field"> whose label is a span, directly wrapping a
            // control. [^<]* — NOT .*? — deliberately: a non-greedy dot under
            // Singleline runs straight past its own </span> to a later one and
            // then matches an unrelated control further down the file. That is the
            // same mistake as the [^>]* label ratchet earlier in this sweep, which
            // flagged 51 pages against the audit's 2.
            foreach (Match match in Regex.Matches(source,
                @"<div class=""simf-field"">\s*<span class=""simf-field__label"">[^<]*</span>\s*<(input|select|textarea)\b",
                RegexOptions.Singleline))
            {
                offenders.Add($"{Relative(file)}:~{source.Take(match.Index).Count(c => c == '\n') + 1}");
            }

            // A <fieldset> named by a span instead of a <legend>.
            foreach (Match match in Regex.Matches(source,
                @"<fieldset[^>]*>\s*<span class=""simf-field__label"">", RegexOptions.Singleline))
            {
                offenders.Add($"{Relative(file)}:~{source.Take(match.Index).Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(offenders.Count == 0,
            "§6.16 (LQ-006/LQ-008): use <label for=\"...\"> for a control and "
            + "<legend> for a fieldset — a span names neither: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void No_stylesheet_hardcodes_a_colour_that_should_follow_the_theme()
    {
        // §6.16 (D3-004 / D3-005 / D3-006) — a raw rgba() literal cannot follow a
        // theme switch. The grid's busy wash was declared white inline and navy in
        // a [data-theme="dark"] override, with NO grey-theme value, so a grid
        // loading over grey surfaces got the white light-theme wash; and two
        // button spinners hardcoded #244A77, which is --color-action in the LIGHT
        // theme only.
        //
        // Scoped to the two files the finding names. theme.tokens.css is where a
        // literal is SUPPOSED to live — that is the whole point of a token file.
        var offenders = new List<string>();

        foreach (var relative in new[]
        {
            "src/Shared/SIMF.Components/wwwroot/css/simf-components.css",
            "src/ControlPanel/SIMF.ControlPanel/Components/Layout/ReconnectModal.razor.css",
        })
        {
            var path = Path.Combine(RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            var source = File.ReadAllText(path);
            foreach (Match match in Regex.Matches(source, @"rgba?\([^)]*\)"))
            {
                // color-mix() derives from a token — that IS the fix, not a defect.
                var lineStart = source.LastIndexOf('\n', match.Index) + 1;
                if (source[lineStart..match.Index].Contains("color-mix", StringComparison.Ordinal)) continue;

                offenders.Add($"{relative}:~{source.Take(match.Index).Count(c => c == '\n') + 1} {match.Value}");
            }
        }

        Assert.True(offenders.Count == 0,
            "§6.16 (D3-004/005/006): this colour is a literal, so it cannot follow "
            + "a theme switch. Add a per-theme token to theme.tokens.css, or derive "
            + "it from one with color-mix(): " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_stylesheet_sets_typography_off_the_scale()
    {
        // §6.16 (D3-003) — typography values bypassed the type scale, including
        // font-weight:600, which is in neither the brand set (300/400/700/800) nor
        // any token.
        //
        // Why 600 mattered: there is no @font-face and no bundled font file, so
        // "FS Albert Arabic" never loads and the CP actually renders in the
        // fallback. Segoe UI DOES ship a real 600 Semibold; Arial and Tahoma do
        // not and synthesize or snap to 700. The same element therefore rendered
        // differently per platform, and would have shifted again — silently — the
        // day the brand font landed without a 600 face.
        //
        // `em` sizes are exempt: they are deliberately RELATIVE to their parent
        // (the country flag glyph, the speaker-card country line) and are not
        // scale steps. theme.tokens.css is exempt because defining the values is
        // its entire job.
        var offenders = new List<string>();

        foreach (var root in new[] { "src/ControlPanel", "src/Shared" })
        {
            var dir = Path.Combine(RepoRoot, root.Replace('/', Path.DirectorySeparatorChar));
            foreach (var file in Directory.EnumerateFiles(dir, "*.css", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                 || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                 || Path.GetFileName(file) == "theme.tokens.css")
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (Match match in Regex.Matches(source,
                    @"font-size:\s*[0-9.]+(rem|px)|font-weight:\s*[0-9]+"))
                {
                    offenders.Add(
                        $"{Relative(file)}:~{source.Take(match.Index).Count(c => c == '\n') + 1} {match.Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "§6.16 (D3-003): this bypasses the type scale. Use a --font-size-* / "
            + "--font-weight-* token, and add the step to theme.tokens.css if it is "
            + "genuinely missing: " + string.Join(", ", offenders));
    }

    // ----------------------------------------------------------------------

    private static IEnumerable<string> CpSources() =>
        Directory.EnumerateFiles(
                Path.Combine(RepoRoot, CpProjectDir.Replace('/', Path.DirectorySeparatorChar)),
                "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Between(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker not found: {startMarker}");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return end < 0 ? source[start..] : source[start..end];
    }

    private static string Relative(string absolute) =>
        Path.GetRelativePath(RepoRoot, absolute).Replace(Path.DirectorySeparatorChar, '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException("Could not locate the SIMF repo root from " + AppContext.BaseDirectory)
            : dir.FullName;
    }
}
