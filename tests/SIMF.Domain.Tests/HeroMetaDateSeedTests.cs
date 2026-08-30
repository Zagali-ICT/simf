using System.Globalization;
using SIMF.Domain.Organization;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// The landing hero's date label is a seeded string, and it stopped being derived.
///
/// <para>Until 2026-08-30 the <c>hero.metadate</c> content block was computed by a
/// C# seeder from <see cref="OrganizationProfile.DefaultEventStart"/> /
/// <see cref="OrganizationProfile.DefaultEventEnd"/> through the shared
/// <c>EventDateRange</c> formatter, so it could not disagree with the edition dates
/// every other surface reads. Moving the content seed to SQL removed that link:
/// <c>SIMF_App_ContentBlocks.sql</c> carries the finished label as a literal,
/// because a SQL script cannot call the formatter.</para>
///
/// <para>The failure that leaves open is quiet and public. Someone moves the
/// edition, every other surface follows the constants, and the marketing landing
/// keeps advertising the old dates to the public until a human notices. Nothing
/// else in the tree compares the two, so this test does - it is the only thing
/// standing between a one-line date change and a wrong date on the front page.</para>
///
/// <para>It deliberately checks the day range and the year rather than
/// re-implementing <c>EventDateRange.Format</c>: SIMF.Domain.Tests does not
/// reference SIMF.Common, and a second copy of the formatting rules would be one
/// more thing to drift. The English month name is checked too, which closes the
/// same-days-different-month case.</para>
/// </summary>
public sealed class HeroMetaDateSeedTests
{
    private const string BlockKey = "hero.metadate";

    [Fact]
    public void The_seeded_hero_date_label_still_matches_the_edition_constants()
    {
        var start = OrganizationProfile.DefaultEventStart;
        var end = OrganizationProfile.DefaultEventEnd;

        // Same month and year is the shape EventDateRange collapses to "23-25
        // November 2026". If the edition ever spans a month boundary the label
        // takes a different shape and this test must be taught the new one -
        // failing here is the correct outcome, not a false alarm.
        Assert.True(
            start.Year == end.Year && start.Month == end.Month,
            $"The edition now spans {start:yyyy-MM-dd}..{end:yyyy-MM-dd}, which "
            + "EventDateRange formats differently from the single-month shape this "
            + "test and the seeded literal both assume. Update "
            + "SIMF_App_ContentBlocks.sql's hero.metadate row and this test together.");

        var row = SeededRow();
        var expectedDays = $"{start.Day}-{end.Day}";
        var expectedYear = start.Year.ToString(CultureInfo.InvariantCulture);
        var expectedMonthEnglish = start.ToString("MMMM", CultureInfo.InvariantCulture);

        Assert.True(
            row.Contains(expectedDays, StringComparison.Ordinal),
            Explain($"the day range '{expectedDays}'", row));
        Assert.True(
            row.Contains(expectedYear, StringComparison.Ordinal),
            Explain($"the year '{expectedYear}'", row));
        Assert.True(
            row.Contains(expectedMonthEnglish, StringComparison.Ordinal),
            Explain($"the English month '{expectedMonthEnglish}'", row));

        static string Explain(string missing, string row) =>
            $"The seeded hero.metadate row does not carry {missing}, so the public "
            + "landing page advertises dates the rest of the system disagrees with. "
            + "OrganizationProfile.DefaultEventStart/End changed and "
            + "docs/migrations/2026/SIMF_App_ContentBlocks.sql was not updated with "
            + "it. Both language values live on that row.\nSeeded row:\n" + row;
    }

    /// <summary>The whole <c>hero.metadate</c> VALUES tuple - English and Arabic
    /// together, because the day range and the year are identical in both (the
    /// formatter emits Western digits in Arabic too) and a single containment
    /// check over the pair therefore covers each.</summary>
    private static string SeededRow()
    {
        var path = Path.Combine(
            RepoRoot(), "docs", "migrations", "2026", "SIMF_App_ContentBlocks.sql");
        Assert.True(File.Exists(path), $"Content-block seed not found at {path}");

        var lines = File.ReadAllLines(path);
        var start = Array.FindIndex(
            lines, line => line.TrimStart().StartsWith($"(N'{BlockKey}'", StringComparison.Ordinal));

        Assert.True(
            start >= 0,
            $"No row for '{BlockKey}' in SIMF_App_ContentBlocks.sql. If the key was "
            + "renamed, LandingHeroContentKeys and the Website proxy changed with it "
            + "and this test needs the new name.");

        // The tuple runs until the line that closes it - ',' for a following row,
        // ');' for the last one in the insert.
        var row = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            row.Add(lines[i]);
            var trimmed = lines[i].TrimEnd();
            if (trimmed.EndsWith("),", StringComparison.Ordinal)
                || trimmed.EndsWith(");", StringComparison.Ordinal))
            {
                break;
            }
        }

        return string.Join('\n', row);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
