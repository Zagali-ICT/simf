// WS0 (QA programme) — structural guards on the per-page E2E catalogue under
// docs/tests/e2e/.
//
// Why a test and not a review checklist: the catalogue is appended to by many
// sessions working in parallel, and nothing about a Markdown table stops two of
// them choosing the same scenario id. That is not a cosmetic clash. Scenario ids
// are the primary key `tools/testbook/build_testbook.py` groups by when it
// projects the catalogue into the human-tester workbook, so a repeated id merges
// two unrelated scenarios into one workbook row — the count still looks right
// while coverage has silently gone missing.
//
// Two real instances were found on 2026-07-28 and fixed in the same change:
//   * `cp-admin-attendance.md` and `cp-admin-attendees.md` both used `E2E-ATT-`
//     with fully overlapping ranges (001..014 against 001..016) — fourteen
//     collisions. Attendance moved to `E2E-ATND-`.
//   * Nine files re-used an id inside a single document, most often because a
//     later batch appended a section without checking what the earlier one took
//     (`cp-admin-halls.md` had FOUR independently-appended sections and two
//     separate "Last reviewed" footers).
//
// The rules below are deliberately structural: a scenario id "counts" only where
// it appears as a Coverage-matrix ROW (`| E2E-XXX-001 | ...`). Prose
// cross-references between pages ("see E2E-DLG-004 on /admin/visitors") are
// legitimate and common, so matching those would produce noise, not signal.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class E2eCatalogueIntegrityTests
{
    /// <summary>A Coverage-matrix row: the id is the first cell of the row.
    ///
    /// <para>The namespace may itself contain a hyphen — two-segment ids like
    /// <c>E2E-BF-01-001</c> (business flows) and the generated element-sweep ids
    /// <c>E2E-HAL-ELS-001</c>. This pattern deliberately matches
    /// <c>tools/testbook/build_testbook.py</c>'s <c>ID_RE</c>: if this test reads
    /// fewer ids than the projector does, it stops guarding exactly the ids the
    /// projector will silently merge, which is the whole point of the
    /// check.</para></summary>
    private static readonly Regex MatrixRow =
        new(@"^\|\s*(E2E-[A-Z0-9][A-Z0-9-]*-\d{3,4})\s*\|", RegexOptions.Compiled);

    /// <summary>The index, the template and the execution playbook are not
    /// per-page catalogues and carry ids only as references.</summary>
    private static readonly HashSet<string> NotCatalogues =
        new(StringComparer.OrdinalIgnoreCase)
        { "README.md", "_TEMPLATE.md", "E2E-TEST-PLAN.md" };

    [Fact]
    public void No_scenario_id_is_claimed_by_two_different_catalogue_files()
    {
        var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (file, ids) in MatrixIdsPerFile())
        {
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                if (!owners.TryGetValue(id, out var list))
                {
                    owners[id] = list = [];
                }
                list.Add(file);
            }
        }

        var collisions = owners
            .Where(pair => pair.Value.Count > 1)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"  {pair.Key} claimed by: {string.Join(", ", pair.Value)}")
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "Two catalogue files claim the same scenario id. The testbook projector "
            + "groups by id, so the colliding rows merge into one workbook entry and "
            + "the rest are dropped without warning. Give one page a distinct "
            + "namespace (as cp-admin-attendance.md did: ATT -> ATND) and update its "
            + "row in docs/tests/e2e/README.md.\n"
            + string.Join('\n', collisions));
    }

    [Fact]
    public void No_catalogue_file_lists_the_same_scenario_id_twice()
    {
        var offenders = new List<string>();
        foreach (var (file, ids) in MatrixIdsPerFile())
        {
            var repeated = ids
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key} x{group.Count()}")
                .ToList();

            if (repeated.Count > 0)
            {
                offenders.Add($"  {file}: {string.Join(", ", repeated)}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A catalogue file lists the same scenario id on more than one matrix row. "
            + "Either two different scenarios share an id (renumber the later one to "
            + "the next free number in that namespace — check the WHOLE file, a page "
            + "may have several appended sections) or one scenario was copied into a "
            + "second table (delete the copy).\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void Every_catalogue_file_is_listed_in_the_index()
    {
        var index = File.ReadAllText(Path.Combine(CatalogueDirectory, "README.md"));
        var missing = CatalogueFiles()
            .Select(Path.GetFileName)
            .Where(name => !index.Contains($"({name})", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "A catalogue file exists but docs/tests/e2e/README.md does not link it, so "
            + "the page is invisible to anyone working from the index and to the "
            + "testbook's coverage roll-up. Add its row (route -> file -> id range).\n"
            + string.Join('\n', missing.Select(name => "  " + name)));
    }

    [Fact]
    public void The_index_roll_up_matches_the_catalogue_it_describes()
    {
        // The roll-up read "74 pages / ~1044 scenarios" while the directory held
        // 183 files and ~2500 scenarios — stale by more than a factor of two, and
        // quoted in planning as if it were current. Pinning it to the real numbers
        // means the next drift fails here instead of misinforming a plan.
        var index = File.ReadAllText(Path.Combine(CatalogueDirectory, "README.md"));
        var claimed = Regex.Match(
            index, @"Pages catalogued:\*\*\s*(?<pages>\d+)\b.*?Total scenarios:\*\*\s*(?<ids>\d+)\b",
            RegexOptions.Singleline);

        Assert.True(
            claimed.Success,
            "docs/tests/e2e/README.md no longer states its roll-up in the expected "
            + "form ('**Pages catalogued:** N' followed by '**Total scenarios:** N'). "
            + "Keep it machine-checkable so it cannot drift silently again.");

        var actualPages = CatalogueFiles().Count;
        var actualScenarios = MatrixIdsPerFile().Sum(entry => entry.Ids.Count);

        Assert.Equal(actualPages, int.Parse(claimed.Groups["pages"].Value));
        Assert.Equal(actualScenarios, int.Parse(claimed.Groups["ids"].Value));
    }

    [Fact]
    public void Every_route_PAGE_INDEX_calls_Real_actually_has_a_page()
    {
        // `/admin/companies` sat in PAGE-INDEX as "✅ Real (D-199)" for eight weeks
        // after a05ef82d renamed the route to /admin/exhibitors and deleted the
        // page — with a 16-scenario catalogue behind it. Nothing failed, because
        // nothing checked. A test round planned from the index would have
        // scheduled those scenarios against a 404.
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
        {
            Path.Combine("src", "ControlPanel", "SIMF.ControlPanel"),
            Path.Combine("src", "Website", "SIMF.Web"),
        })
        {
            var directory = Path.Combine(FindRepoRoot(), root);
            foreach (var razor in Directory.EnumerateFiles(directory, "*.razor", SearchOption.AllDirectories))
            {
                foreach (Match page in Regex.Matches(
                    File.ReadAllText(razor), @"^@page\s+""([^""]+)""", RegexOptions.Multiline))
                {
                    declared.Add(NormaliseRoute(page.Groups[1].Value));
                }
            }
        }

        var indexPath = Path.Combine(FindRepoRoot(), "docs", "pages", "PAGE-INDEX.md");
        var broken = new List<string>();
        foreach (var line in File.ReadLines(indexPath))
        {
            // First cell = the route the row is about; second = its status. The
            // status must BEGIN with the ✅ marker to count as a live claim —
            // matching "Real" anywhere in the cell would also fire on a retired
            // row that quotes the marker it used to carry.
            var row = Regex.Match(line, @"^\|\s*`(/[^`]*)`\s*\|\s*(✅\s*Real[^|]*)\|");
            if (!row.Success)
            {
                continue;
            }
            var route = NormaliseRoute(row.Groups[1].Value);
            if (!declared.Contains(route))
            {
                broken.Add($"  {row.Groups[1].Value} — marked Real, but no @page declares it");
            }
        }

        Assert.True(
            broken.Count == 0,
            "docs/pages/PAGE-INDEX.md marks a route \"Real\" that no .razor @page "
            + "declares. Either the page was renamed or removed and the row was left "
            + "behind (retire the row and point it at the successor), or the route "
            + "moved and the row needs updating.\n"
            + string.Join('\n', broken));
    }

    /// <summary>Route parameters vary in spelling (`{id}` / `{id:guid}` /
    /// `{slug?}`), so compare on the shape rather than the literal text.</summary>
    private static string NormaliseRoute(string route) =>
        Regex.Replace(route.TrimEnd('/'), @"\{[^}]*\}", "{}");

    // ----------------------------------------------------------------------

    private static string CatalogueDirectory =>
        Path.Combine(FindRepoRoot(), "docs", "tests", "e2e");

    private static List<string> CatalogueFiles() =>
        [.. Directory.EnumerateFiles(CatalogueDirectory, "*.md")
            .Where(path => !NotCatalogues.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)];

    private static List<(string File, List<string> Ids)> MatrixIdsPerFile()
    {
        var result = new List<(string, List<string>)>();
        foreach (var path in CatalogueFiles())
        {
            var ids = new List<string>();
            foreach (var line in File.ReadLines(path))
            {
                var match = MatrixRow.Match(line);
                if (match.Success)
                {
                    ids.Add(match.Groups[1].Value);
                }
            }
            result.Add((Path.GetFileName(path), ids));
        }
        return result;
    }

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
