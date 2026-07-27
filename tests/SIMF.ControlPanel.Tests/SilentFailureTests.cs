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
