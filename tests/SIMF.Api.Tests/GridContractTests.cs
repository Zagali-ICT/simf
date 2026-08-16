// Tests: this IS the test.
//
// The Control Panel and the backend each held their own idea of a column key, and
// nothing checked that the two agreed. Three wrong-sort bugs shipped that way and
// were each found by eye, weeks later: a page marked a column Sortable, the service
// switched on a key the page never sent, the arrow moved and the rows did not.
// Five filter boxes were dead in the same way, and a dead filter box is worse than a
// dead sort — it returns the UNFILTERED set, which on an admin grid over people
// reads as a result rather than a fault.
//
// This walks the razor files and the services and joins them on the row DTO, so the
// disagreement fails the build instead of waiting for someone to notice. It is the
// same shape as BusinessFlow13PermissionMatrixTests: enumerate reality, compare it
// against what the code declares, and make a new entry argue for itself.
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed partial class GridContractTests
{
    [Fact]
    public void Every_sortable_or_filterable_control_panel_column_is_declared_by_its_service()
    {
        var declaredByRowType = DeclaredKeysByRowType();
        var unmatched = new List<string>();

        foreach (var (razor, rowType, keys) in ControlPanelGrids())
        {
            // A grid whose row type no service declares columns for is not converted
            // yet (or is client-side only). Those are covered by the ratchet below,
            // not here, so that this test fails only on a genuine disagreement.
            if (!declaredByRowType.TryGetValue(rowType, out var declared))
            {
                continue;
            }

            foreach (var key in keys.Where(key => !declared.Contains(key)))
            {
                unmatched.Add($"  {razor}: <SimfDataGridColumn TItem=\"{rowType}\" Key=\"{key}\"> "
                    + $"is Sortable/Filterable, but no service declaring GridColumns for "
                    + $"{rowType} declares '{key}'. The grid would send it and get a 400.");
            }
        }

        Assert.True(
            unmatched.Count == 0,
            "Control Panel column keys with no server declaration:\n"
            + string.Join("\n", unmatched));
    }

    [Fact]
    public void The_number_of_hand_written_sort_switches_only_goes_down()
    {
        // The seam replaced 45 of these. The ones left are deliberate: they resolve
        // across BOTH DbContexts, so the id list has to be built before the paged
        // query can be composed and there is no single IQueryable to hand over
        // (D-157 forbids the join). InterestRepository is a repository, not a list
        // service. Any NEW entry here is a regression: it means someone hand-rolled
        // a filter/sort block instead of declaring columns.
        string[] allowed =
        [
            "AdminAccountService.cs",
            "AdminAttendeeService.cs",
            "AdminInvitationService.cs",
            "InterestRepository.cs",
            "SeatReservationService.cs",
        ];

        var infrastructure = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Infrastructure");

        var handWritten = Directory
            .EnumerateFiles(infrastructure, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("Persistence", "Migrations"), StringComparison.Ordinal))
            .Where(path => SortSwitch().IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var unexpected = handWritten.Except(allowed, StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            "These services hand-write a sort switch instead of declaring GridColumns:\n  "
            + string.Join("\n  ", unexpected)
            + "\nDeclare the columns and call ToGridPageAsync; see AdminThemeService.");
    }

    [Fact]
    public void No_grid_column_is_declared_over_an_encrypted_column()
    {
        // A column over a randomized AES-GCM property compiles, translates, runs —
        // and matches nothing, forever, because the term is encrypted with a fresh
        // nonce and compared against differently-nonced ciphertext. On a people grid
        // "no rows" reads as "not registered", which is worse than an error, and no
        // translation smoke test catches it because translation SUCCEEDS.
        string[] encrypted = ["MobileNumber", "SaudiMobile", "InternationalMobile"];

        var infrastructure = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Infrastructure");
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(infrastructure, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in ColumnDeclaration().Matches(File.ReadAllText(path)))
            {
                var property = match.Groups["prop"].Value;
                if (encrypted.Contains(property, StringComparer.Ordinal))
                {
                    offenders.Add(
                        $"  {Path.GetFileName(path)}: '{match.Groups["key"].Value}' -> {property}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Grid columns declared over a value-converted (encrypted) property:\n"
            + string.Join("\n", offenders)
            + "\nThese never match. Filter on a companion column instead.");
    }

    // ---- reading what each side actually declares --------------------------------

    /// <summary>Row DTO type -> every column key any service declares for it.</summary>
    private static Dictionary<string, HashSet<string>> DeclaredKeysByRowType()
    {
        var infrastructure = Path.Combine(RepoRoot(), "src", "Backend", "SIMF.Infrastructure");
        var byRowType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(infrastructure, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            if (!source.Contains("GridColumns<", StringComparison.Ordinal))
            {
                continue;
            }

            // A file's declared keys are pooled across its list methods. That is
            // deliberately loose: a service with two grids (rating groups and rating
            // questions) would otherwise need the test to parse method bodies, and
            // the failure this guards — a key NO service declares — is caught either
            // way.
            var keys = ColumnDeclaration()
                .Matches(source)
                .Select(match => match.Groups["key"].Value)
                .Concat(FilterDeclaration().Matches(source).Select(match => match.Groups["key"].Value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in RowType().Matches(source))
            {
                var rowType = match.Groups["row"].Value;
                if (!byRowType.TryGetValue(rowType, out var existing))
                {
                    // Explicitly OrdinalIgnoreCase, not a collection expression: a
                    // spread would build a fresh set with the DEFAULT case-sensitive
                    // comparer and silently drop the one thing this comparison needs.
                    // The pages genuinely send "namearabic" where the service declares
                    // "nameArabic", and the seam binds them case-insensitively.
                    byRowType[rowType] = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                existing.UnionWith(keys);
            }
        }

        return byRowType;
    }

    /// <summary>Every CP grid column that carries Sortable or Filterable, with the
    /// row type of the grid it sits in.</summary>
    private static List<(string Razor, string RowType, List<string> Keys)> ControlPanelGrids()
    {
        var pages = Path.Combine(
            RepoRoot(), "src", "ControlPanel", "SIMF.ControlPanel", "Components", "Pages");

        var grids = new List<(string, string, List<string>)>();

        foreach (var path in Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories))
        {
            var byRowType = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (Match match in GridColumnTag().Matches(File.ReadAllText(path)))
            {
                var tag = match.Value;
                if (!tag.Contains("Sortable", StringComparison.Ordinal)
                    && !tag.Contains("Filterable", StringComparison.Ordinal))
                {
                    continue;
                }

                var rowType = match.Groups["item"].Value;
                var key = match.Groups["key"].Value;
                if (rowType.Length == 0 || key.Length == 0)
                {
                    continue;
                }

                if (!byRowType.TryGetValue(rowType, out var keys))
                {
                    byRowType[rowType] = keys = [];
                }
                keys.Add(key);
            }

            grids.AddRange(byRowType.Select(entry =>
                (Path.GetFileName(path), entry.Key, entry.Value)));
        }

        return grids;
    }

    // `.Add("key", x => x.Property` — the trailing selector is what lets the
    // encrypted-column check name the property, so a computed selector simply does
    // not match and is reviewed as code instead.
    [GeneratedRegex(@"\.Add\(\s*""(?<key>[^""]+)""\s*,\s*\w+\s*=>\s*\w+\.(?<prop>\w+)")]
    private static partial Regex ColumnDeclaration();

    [GeneratedRegex(@"\.AddFilter\(\s*""(?<key>[^""]+)""")]
    private static partial Regex FilterDeclaration();

    [GeneratedRegex(@"GridPage<(?<row>\w+)>")]
    private static partial Regex RowType();

    [GeneratedRegex(@"<SimfDataGridColumn\b[^>]*?TItem=""(?<item>\w+)""[^>]*?Key=""(?<key>[^""]+)""[^>]*>",
        RegexOptions.Singleline)]
    private static partial Regex GridColumnTag();

    [GeneratedRegex(@"query\.Sort\?\.ToLowerInvariant\(\)")]
    private static partial Regex SortSwitch();

    // The test project runs from a deep bin directory, so walk upward to the root.
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
