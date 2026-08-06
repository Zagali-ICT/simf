using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// Guards <c>docs/decisions/DECISIONS_LOG.md</c>, which the comment standard
/// depends on: source comments were stripped of decision ids on the promise that
/// the reasoning is recoverable from the log. That promise is only as good as the
/// log's ability to resolve an id to exactly one decision.
///
/// <para>Two real defects motivated this. Rows were twice written so that several
/// decisions shared one line and only the first kept an ID cell, leaving four ids
/// resolving to nothing. And two dated batches were numbered from the same
/// starting point, so 54 ids each label two unrelated decisions — a citation of
/// one of those is ambiguous, not merely untidy. Both are frozen here as counted
/// baselines: they may shrink, never grow.</para>
/// </summary>
public sealed class DecisionsLogIntegrityTests
{
    private static readonly Regex RowStart =
        new(@"^\|\s*(?<id>D-\d{3})\s*\|\s*(?<date>\d{4}-\d{2}-\d{2})\s*\|", RegexOptions.Compiled);

    // A second date cell after the first is the signature of the merged-row
    // defect: two decisions concatenated onto one line, the later one losing its
    // ID cell and becoming unreachable by id.
    private static readonly Regex ExtraDateCell =
        new(@"\|\s*\d{4}-\d{2}-\d{2}\s*\|", RegexOptions.Compiled);

    /// <summary>Ids that label two unrelated decisions, measured 2026-08-06.
    /// Resolving a collision means renumbering one side, which invalidates
    /// citations in commit messages that cannot be rewritten — an owner decision,
    /// deliberately not taken here. Shrinking this set is welcome; growing it
    /// means a new batch was numbered over an existing one.</summary>
    private static readonly string[] KnownCollidingIds =
    [
        "D-587", "D-588", "D-589", "D-590", "D-591", "D-592", "D-593", "D-594", "D-595",
        "D-596", "D-597", "D-598", "D-599", "D-600", "D-601", "D-602", "D-603",
        "D-604", "D-605", "D-606", "D-607", "D-608", "D-609", "D-610", "D-611",
        "D-612", "D-613", "D-614", "D-615", "D-616", "D-617", "D-618", "D-619",
        "D-620", "D-621", "D-638", "D-639", "D-640", "D-641", "D-642", "D-643",
        "D-644", "D-645", "D-646", "D-647", "D-648", "D-649", "D-650", "D-756",
        "D-760", "D-761", "D-771", "D-772", "D-773", "D-774",
    ];

    [Fact]
    public void Every_decision_row_carries_its_own_id_and_date()
    {
        var offenders = Rows()
            .Where(row => ExtraDateCell.IsMatch(row.Line[row.Match.Length..]))
            .Select(row => $"{row.Match.Groups["id"].Value} (line {row.Number})")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A decisions-log row contains a second date cell, which means two or more "
            + "decisions were concatenated onto one line and every one after the first "
            + "lost its ID cell — those ids then resolve to nothing, which defeats the "
            + "whole reason source comments cite the log instead of repeating it. "
            + "Give each decision its own row. Offending rows: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void No_new_id_labels_two_different_decisions()
    {
        var colliding = Rows()
            .GroupBy(row => row.Match.Groups["id"].Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var added = colliding.Except(KnownCollidingIds).ToArray();
        var resolved = KnownCollidingIds.Except(colliding).ToArray();

        Assert.True(
            added.Length == 0,
            "These ids now label more than one decision, so a citation of them is "
            + "ambiguous: " + string.Join(", ", added)
            + ". A new decision must take the next UNUSED id — check the top of the "
            + "table, not the top of your branch.");

        Assert.True(
            resolved.Length == 0,
            "Collisions were resolved for " + string.Join(", ", resolved)
            + " — good. Remove them from KnownCollidingIds so the baseline keeps "
            + "describing reality.");
    }

    [Fact]
    public void The_newest_decision_uses_the_highest_id()
    {
        // The table is maintained newest-first, so the first row should also carry
        // the largest id. When it does not, a branch has numbered backwards into
        // territory another branch already used - which is how the 54 collisions
        // above were created in the first place.
        var rows = Rows().ToList();
        Assert.NotEmpty(rows);

        var first = int.Parse(rows[0].Match.Groups["id"].Value[2..]);
        var highest = rows.Max(row => int.Parse(row.Match.Groups["id"].Value[2..]));

        Assert.True(
            first == highest,
            $"The top row is D-{first:000} but the highest id in the table is "
            + $"D-{highest:000}. Newest-first ordering is what makes \"take the next "
            + "id\" a one-glance operation; break it and the next author picks a "
            + "number that is already taken.");
    }

    private static IEnumerable<(string Line, int Number, Match Match)> Rows()
    {
        var path = Path.Combine(RepoRoot(), "docs", "decisions", "DECISIONS_LOG.md");
        Assert.True(File.Exists(path), $"Decisions log not found at {path}");

        var number = 0;
        foreach (var line in File.ReadLines(path))
        {
            number++;
            var match = RowStart.Match(line);
            if (match.Success)
            {
                yield return (line, number, match);
            }
        }
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
