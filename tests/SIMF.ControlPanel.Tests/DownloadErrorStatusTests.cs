// Guards the fix for: a bodiless error status on a binary download is rewritten
// to 400 + HTML before it reaches the caller.
//
// Program.cs runs UseStatusCodePagesWithReExecute("/not-found"). That middleware
// re-executes ANY error status whose response has no body. Results.StatusCode(x)
// writes exactly such a response, so a clean 403 from the API arrived at the
// browser as 400 with an HTML page. The export was still correctly denied - no
// data leaked - but the caller was told the wrong thing, and
// simfAccount.downloadXlsx could not parse the HTML so it synthesised
// BAD_RESPONSE instead of showing the real "you may not export this" message.
//
// Proven live on the QA stack, signed in as the restricted admin fixture:
//   before  POST /account/api/admin/reports/partners/export -> 400 text/html
//   after   POST /account/api/admin/reports/partners/export -> 403 application/json
//                                                             {"error":{"code":"FORBIDDEN",...}}
//
// AccountEndpoints is `internal` and the test project has no InternalsVisibleTo,
// so this asserts on the source the way the repo's other structural guards do
// (PipelineTestGateTests, E2eCatalogueIntegrityTests, the Flutter ratchets).
using System.Text.RegularExpressions;

namespace SIMF.ControlPanel.Tests;

public sealed class DownloadErrorStatusTests
{
    /// <summary>
    /// Anchored on the Endpoints folder itself, NOT on "the directory containing
    /// .git". In a git WORKTREE `.git` is a FILE, so that walk skips the
    /// worktree root and finds the main checkout — the scan then reports on a
    /// different tree than the one being built, and passes or fails for reasons
    /// that have nothing to do with the code under test.
    /// </summary>
    private static string EndpointsDirectory()
    {
        var relative = Path.Combine("src", "ControlPanel", "SIMF.ControlPanel", "Endpoints");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, $"Could not find '{relative}' above {AppContext.BaseDirectory}");
        return Path.Combine(dir!.FullName, relative);
    }

    /// <summary>Lines that RETURN a bodiless status. Doc comments mentioning the
    /// pattern are excluded — the fix's own explanation names it, and a naive
    /// text match counts that comment as an offender.</summary>
    private static List<string> BodilessStatusSites()
    {
        var sites = new List<string>();
        foreach (var file in Directory.EnumerateFiles(
                     EndpointsDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("///", StringComparison.Ordinal)) { continue; }
                if (Regex.IsMatch(line, @"return\s+Results\.StatusCode\(status\)"))
                {
                    sites.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        return sites;
    }

    [Fact]
    public void The_export_helpers_do_not_return_a_bodiless_status()
    {
        // The two shared helpers cover every XLSX download in the Control Panel:
        // ForwardReportExportAsync (the 8 reports) and MapGridExport (13 grid
        // slugs). If either returns a bodiless status again, every one of those
        // 21 endpoints silently reports the wrong code on its deny path.
        var source = File.ReadAllText(
            Path.Combine(EndpointsDirectory(), "AccountEndpoints.cs"));

        var offenders = BodilessStatusSites()
            .Where(s => s.StartsWith("AccountEndpoints.cs:", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "AccountEndpoints.cs returns a bodiless status again. "
            + "UseStatusCodePagesWithReExecute(\"/not-found\") re-executes it, so the "
            + "caller receives 400 + HTML instead of the real status. Return "
            + "DownloadFailure(status, bytes) instead — the API's own bilingual "
            + "ApiResult error is already in `bytes`.\n"
            + string.Join('\n', offenders));

        // Both helpers must actually route through it, not just avoid the old call.
        Assert.Equal(2, Regex.Matches(source, @"return DownloadFailure\(status, bytes\)").Count);
    }

    [Fact]
    public void No_endpoint_returns_a_bodiless_status()
    {
        // Was a baseline of 16 tolerated sites across UserDocuments (10),
        // MediaAndPartners, FaqAndRoles, Gates and SelfService. All 16 are now
        // converted, so the ratchet is at ZERO and this is a plain rule rather
        // than a debt counter.
        //
        // Every one of the 16 was audited before conversion: `bytes` in scope at
        // all of them, and — the load-bearing fact — EVERY guard can fire while
        // status == 200 (they read `status != 200 || contentType is null ||
        // bytes.Length == 0`). That is why DownloadFailure returns a bodiless 200
        // untouched: giving 200 a JSON body would read as success, and where
        // contentType is null but bytes are present it would serve raw image
        // bytes labelled application/json.
        var sites = BodilessStatusSites();

        Assert.True(
            sites.Count == 0,
            $"{sites.Count} endpoint(s) return a bodiless status. "
            + "UseStatusCodePagesWithReExecute(\"/not-found\") re-executes any bodiless "
            + "4xx/5xx, so the caller receives 400 + HTML instead of the real status. "
            + "Return DownloadFailure(status, bytes) instead — it passes the upstream "
            + "error body through with the true status, and leaves 200 alone.\n"
            + string.Join('\n', sites));
    }

    [Fact]
    public void Every_download_failure_path_routes_through_the_helper()
    {
        // The count is asserted, not just the absence of the old call, so a new
        // download endpoint that invents its own bodiless failure path is caught
        // even if it never types Results.StatusCode(status).
        var uses = Directory
            .EnumerateFiles(EndpointsDirectory(), "*.cs", SearchOption.AllDirectories)
            .Sum(f => Regex.Matches(
                File.ReadAllText(f), @"return DownloadFailure\(status, bytes\);").Count);

        Assert.Equal(18, uses);
    }
}
