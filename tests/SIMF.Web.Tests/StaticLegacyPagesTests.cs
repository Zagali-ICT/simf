// Tests: #40-residual — a hand-written static page had been left under
// SIMF.Web/wwwroot after the Blazor route that replaced it shipped. Because
// wwwroot is served verbatim, /speakers.html stayed publicly reachable next to
// the real /speakers route — and it carried the event dates as a hardcoded
// literal ("23–25 نوفمبر 2026"), so the CP-editable edition dates could never
// reach it. The file has been deleted.
//
// These are ratchet tests. Both assertions FAIL on the pre-fix tree (speakers.html
// was present and matched the date pattern) and hold afterwards, so re-adding a
// static duplicate of a Blazor page — or hardcoding the event dates into one —
// breaks the build instead of shipping a page nobody remembers to update.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Web.Tests;

public sealed class StaticLegacyPagesTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string WebWwwroot = "src/Website/SIMF.Web/wwwroot";

    /// <summary>A "23-25 November 2026" / "23–25 نوفمبر 2026" style literal — a day
    /// range, a Gregorian month name in either language, and a 20xx year.</summary>
    private static readonly Regex HardcodedEventDate = new(
        @"\d{1,2}\s*[-–—]\s*\d{1,2}\s+"
        + @"(نوفمبر|ديسمبر|يناير|فبراير|مارس|أبريل|مايو|يونيو|يوليو|أغسطس|سبتمبر|أكتوبر"
        + @"|Nov(ember)?|Dec(ember)?|Jan(uary)?|Feb(ruary)?|Mar(ch)?|Apr(il)?|May"
        + @"|Jun(e)?|Jul(y)?|Aug(ust)?|Sep(tember)?|Oct(ober)?)\s+20\d{2}",
        RegexOptions.Compiled);

    [Fact]
    public void The_legacy_static_speakers_page_is_gone()
    {
        var legacy = Path.Combine(
            RepoRoot,
            WebWwwroot.Replace('/', Path.DirectorySeparatorChar),
            "speakers.html");

        Assert.False(
            File.Exists(legacy),
            "wwwroot/speakers.html is served verbatim at /speakers.html, so it shadows "
            + "nothing but stays publicly reachable beside the Blazor /speakers route "
            + "that replaced it. Delete the static page rather than maintaining two.");
    }

    [Fact]
    public void No_static_website_page_hardcodes_the_event_dates()
    {
        var wwwroot = Path.Combine(
            RepoRoot, WebWwwroot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(wwwroot))
        {
            return;
        }

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(wwwroot, "*.html", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            // The Blazor boot artefacts are generated, not authored here.
            if (relative.Contains("/_framework/", StringComparison.Ordinal)
                || relative.Contains("/_content/", StringComparison.Ordinal))
            {
                continue;
            }
            if (HardcodedEventDate.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The forum dates are CP-editable (OrganizationProfile.EventStartDate / "
            + "EventEndDate) and are rendered through ForumDates. A static page cannot "
            + "read them, so a hardcoded range there goes stale the moment the edition "
            + "changes. Offending files: " + string.Join(", ", offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SIMF.slnx")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate the SIMF repo root from " + AppContext.BaseDirectory);
        }
        return dir.FullName;
    }
}
