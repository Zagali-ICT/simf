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
    private static string EndpointsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var endpoints = Path.Combine(
            dir!.FullName, "src", "ControlPanel", "SIMF.ControlPanel", "Endpoints");
        Assert.True(Directory.Exists(endpoints), $"Endpoints directory not found at {endpoints}");
        return endpoints;
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
    public void The_remaining_bodiless_sites_do_not_grow()
    {
        // 16 sites in UserDocuments, MediaAndPartners, FaqAndRoles, Gates and
        // SelfService carry the SAME defect. They are document and media download
        // paths that were not driven when this was found, so they are recorded
        // rather than changed blind — but the count must not grow. A new one is a
        // new endpoint that will misreport its own failures.
        const int knownSites = 16;

        var sites = BodilessStatusSites();

        Assert.True(
            sites.Count <= knownSites,
            $"A new bodiless error status appeared ({sites.Count} > {knownSites}). "
            + "Use DownloadFailure(status, bytes) so the response carries a body and "
            + "UseStatusCodePagesWithReExecute cannot rewrite it to 400 + HTML.\n"
            + string.Join('\n', sites));

        Assert.True(
            sites.Count == knownSites,
            $"{knownSites - sites.Count} of the known bodiless sites were fixed — "
            + "lower `knownSites` to " + sites.Count + " so the ratchet holds the gain.");
    }
}
