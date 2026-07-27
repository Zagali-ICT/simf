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
