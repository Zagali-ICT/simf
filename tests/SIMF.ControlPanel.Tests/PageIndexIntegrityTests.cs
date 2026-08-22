// Structural guard on docs/pages/PAGE-INDEX.md: no route may be described twice.
//
// Why a test and not a clean-up. On 2026-08-20 the Mobile section carried 15 rows
// covering 7 routes. Git history says how, and it is not carelessness: EVERY
// duplication event was a merge commit and every de-duplication was an ordinary
// one.
//
//   e225c39e  merge 'feat/qa-seat-tiers-and-staff-seating'   staffRegisterVisitor 1 -> 2
//   74e4d663  merge 'fix/final-exhibitor-500-and-phone'                           2 -> 3
//   f5f0236a  feat(cp): programme dashboard   (NOT a merge)                       3 -> 1
//   c1edaa39  merge 'origin/main'                                                 1 -> 3
//
// Two branches each append a row for the same route at different offsets in a long
// table. Git sees insertions at non-overlapping line ranges, so there is no
// conflict to resolve and it keeps both — nobody skipped a marker, because nobody
// was shown one. f5f0236a is the proof that a manual fix does not hold: someone
// cleaned staffRegisterVisitor back to one row and the very next merge from main
// restored all three.
//
// The duplicates are worse than untidy because the copies DISAGREE. They were
// written at different times by different branches, so a reader who hits the wrong
// one first gets wrong information with no hint that a better row sits two lines
// below. Both of these were live on 2026-08-20:
//   * two `myVisitors` copies documented `/app/exhibitor/my-visitors` and one
//     documented `/app/exhibitor/visitors`. Only the third exists.
//   * every `scanVisitor` copy documented `POST /app/exhibitor/scan`. The real
//     route is `/app/exhibitor/visitors/scan`, so all three were wrong.
//   * one `badge` copy recorded a DEF-EXH-005 fix; the other silently dropped it.
//
// This fires on the SECOND row for a route key, which is the moment a merge
// creates the ambiguity, rather than waiting for someone to notice the two rows
// have drifted apart.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class PageIndexIntegrityTests
{
    // The first cell of a route row: an optional mockup number (`#32 `) then the
    // route name in backticks. Rows whose first cell is a PATH (`/admin/...`) are
    // the Control Panel's own convention and are matched by the sibling
    // E2eCatalogueIntegrityTests; this guard is about the named-route rows.
    private static readonly Regex RouteRow = new(
        @"^\|\s*(?:#\d+\s*)?`(?<route>[a-zA-Z][a-zA-Z0-9]*)`",
        RegexOptions.Compiled);

    [Fact]
    public void No_route_is_described_by_more_than_one_row()
    {
        var lines = File.ReadAllLines(PageIndexPath());
        var rowsPerRoute = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var match = RouteRow.Match(lines[i].TrimStart());
            if (!match.Success)
            {
                continue;
            }

            var route = match.Groups["route"].Value;
            if (!rowsPerRoute.TryGetValue(route, out var at))
            {
                at = new List<int>();
                rowsPerRoute[route] = at;
            }

            at.Add(i + 1);
        }

        var duplicated = rowsPerRoute
            .Where(pair => pair.Value.Count > 1)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"  {pair.Key}: lines {string.Join(", ", pair.Value)}")
            .ToList();

        Assert.True(
            duplicated.Count == 0,
            "docs/pages/PAGE-INDEX.md describes these routes more than once:\n"
            + string.Join("\n", duplicated)
            + "\n\nThis is almost always a merge keeping both sides: two branches each "
            + "appended a row for the route at different offsets, so git saw no conflict. "
            + "Do NOT simply delete the extra rows — the copies were written at different "
            + "times and routinely disagree, so one of them carries a fact the other lost. "
            + "Read every copy, write a single row carrying the union, and verify any API "
            + "path it cites against src/Backend/SIMF.Api/Endpoints/ before you keep it.");
    }

    private static string PageIndexPath() =>
        Path.Combine(FindRepoRoot(), "docs", "pages", "PAGE-INDEX.md");

    // Anchors on SIMF.slnx rather than on `.git`, matching the sibling guards. In a
    // git worktree `.git` is a FILE, not a directory, and a check that looks for the
    // directory walks past the real root and audits the wrong tree — which is how
    // tool/conventions once passed vacuously.
    private static string FindRepoRoot()
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
