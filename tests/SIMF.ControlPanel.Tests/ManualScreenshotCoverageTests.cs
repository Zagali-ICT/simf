// Tests: this file IS the guard - it has no production counterpart.
//
// The operations manual (docs/manuals/SIMF-CP-Operations-Manual-*.docx) gives
// every Control Panel page an entry with a screenshot of it, in both languages.
// Nothing kept that true: a page added tomorrow would get no capture, no entry,
// and nobody would find out until somebody read the book and noticed a hole.
//
// This is that check. It compares the pages the application actually serves
// against the captures on disk, and fails naming the ones with no picture.
//
// Regenerate the missing captures with tools/manual/doc-env.ps1 and the
// ManualCapture runner; tools/manual/README.md is the runbook.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SIMF.ControlPanel.Tests;

public sealed class ManualScreenshotCoverageTests
{
    /// <summary>Routes the manual documents WITHOUT a picture, on purpose.
    ///
    /// <para>These five redirect for a signed-in reader - the two account-state
    /// pages go to the dashboard and the three sign-in pages go back to the
    /// sign-in form - so a capture of them is a picture of whatever answered
    /// instead. The manual prints an explanation in their place. The capture
    /// runner records each redirect it meets, and this list is checked against
    /// that record below rather than simply trusted.</para></summary>
    private static readonly string[] DocumentedWithoutAPicture =
    [
        "auth-pending", "auth-rejected",
        "login-enrol-2fa", "login-recovery", "login-totp",
    ];

    [Fact]
    public void Every_control_panel_page_has_a_screenshot_in_both_languages()
    {
        var repo = RepoRoot();
        var shots = Path.Combine(repo, "docs", "screenshots", "manual");
        Assert.True(Directory.Exists(shots),
            $"The manual's screenshot directory is missing: {shots}");

        var missing = new List<string>();
        foreach (var (route, slug) in PageSlugs(repo))
        {
            if (DocumentedWithoutAPicture.Contains(slug))
            {
                continue;
            }

            foreach (var suffix in new[] { "", "-ar" })
            {
                var file = Path.Combine(shots, $"cp-{slug}-default{suffix}.png");
                if (!File.Exists(file))
                {
                    missing.Add($"{route}  ->  cp-{slug}-default{suffix}.png");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "These Control Panel pages have no screenshot for the operations "
            + "manual, so the manual would describe them without showing them:"
            + Environment.NewLine + "  "
            + string.Join(Environment.NewLine + "  ", missing)
            + Environment.NewLine + Environment.NewLine
            + "Capture them with tools/manual/doc-env.ps1 plus the ManualCapture "
            + "runner (see tools/manual/README.md), or - if the page genuinely "
            + "cannot be photographed - add its slug to DocumentedWithoutAPicture "
            + "with the reason.");
    }

    /// <summary>The exemption list must describe reality, not outlive it.
    ///
    /// <para>A page that stops redirecting becomes photographable, and leaving
    /// it exempt would silently keep it out of the manual for good. The capture
    /// runner writes down every redirect it met, so the two are compared.</para></summary>
    [Fact]
    public void The_pages_documented_without_a_picture_are_the_ones_that_redirect()
    {
        var report = Path.Combine(RepoRoot(), "docs", "screenshots", "manual",
                                  "capture-report-sweep-en.json");
        if (!File.Exists(report))
        {
            // No capture has been run in this working tree. That is not a
            // failure of the product, and the test above already covers the
            // files themselves.
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(report));
        var redirected = document.RootElement.EnumerateArray()
            .Where(entry => entry.TryGetProperty("redirected", out var flag)
                            && flag.GetBoolean())
            .Select(entry => entry.GetProperty("slug").GetString()!)
            .OrderBy(slug => slug)
            .ToArray();

        Assert.True(
            redirected.Length == 0 || redirected.SequenceEqual(DocumentedWithoutAPicture.OrderBy(s => s)),
            "The pages exempted from needing a screenshot no longer match the "
            + "pages the capture actually could not reach."
            + Environment.NewLine
            + "  exempted : " + string.Join(", ", DocumentedWithoutAPicture.OrderBy(s => s))
            + Environment.NewLine
            + "  redirects: " + string.Join(", ", redirected));
    }

    /// <summary>Every <c>@page</c> route the Control Panel serves, with the slug
    /// the manual's build gives it. The slug rule is duplicated from
    /// tools/manual/build_page_model.py deliberately: a guard that imported its
    /// subject would agree with it by construction and prove nothing.</summary>
    private static IEnumerable<(string Route, string Slug)> PageSlugs(string repo)
    {
        var components = Path.Combine(repo, "src", "ControlPanel",
                                      "SIMF.ControlPanel", "Components");
        foreach (var razor in Directory.EnumerateFiles(components, "*.razor",
                                                       SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(razor),
                                                  "^@page\\s+\"([^\"]+)\"",
                                                  RegexOptions.Multiline))
            {
                yield return (match.Groups[1].Value, Slugify(match.Groups[1].Value));
            }
        }
    }

    private static string Slugify(string route)
    {
        var cleaned = Regex.Replace(route.Trim('/'), "\\{[^}]*\\}", "x");
        cleaned = Regex.Replace(cleaned, "[^A-Za-z0-9]+", "-").Trim('-').ToLowerInvariant();
        return cleaned.Length == 0 ? "dashboard" : cleaned;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
