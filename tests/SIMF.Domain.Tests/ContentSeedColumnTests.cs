using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// Every column named by a content seed must still exist on the entity it
/// inserts into.
///
/// <para>This test exists because the same mistake landed twice, and both times
/// it cost a full test run to find. A retired column left in an
/// <c>INSERT INTO … (cols)</c> list fails the statement outright — passing
/// <c>NULL</c> does not save it, because SQL Server rejects the column list
/// before it looks at a single value. The seeds run when the API test fixture
/// builds its database, so one stale name does not fail one test: it fails
/// every integration test at once, 2,067 of them the first time, with an error
/// that names a seed file rather than the change that broke it.</para>
///
/// <para>It compares against entity PROPERTY names, which is sound here because
/// no configuration renames a column — <c>HasColumnName</c> appears nowhere in
/// the persistence layer, so EF's convention makes the two identical. If that
/// ever stops being true, this test will say so by failing on a column that is
/// really fine, which is the safe direction to be wrong in.</para>
/// </summary>
public sealed class ContentSeedColumnTests
{
    // INSERT INTO dbo.Table (a, b, c)
    private static readonly Regex InsertColumns = new(
        @"INSERT\s+INTO\s+dbo\.(?<table>\w+)\s*\((?<cols>[^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Tables a seed writes that are not EF entities of their own —
    /// join tables and lookups whose columns cannot be checked this way. Listed
    /// rather than silently skipped, so the gap is visible.</summary>
    private static readonly HashSet<string> NotEntityBacked = new(StringComparer.OrdinalIgnoreCase)
    {
        "SessionThemes",
        "SessionSpeakers",
    };

    [Fact]
    public void Every_seeded_column_still_exists_on_its_entity()
    {
        var entities = EntityPropertiesByTypeName();
        var offenders = new List<string>();

        foreach (var file in SeedFiles())
        {
            var sql = File.ReadAllText(file);
            foreach (Match match in InsertColumns.Matches(sql))
            {
                var table = match.Groups["table"].Value;
                if (NotEntityBacked.Contains(table))
                {
                    continue;
                }
                if (!TryFindEntity(entities, table, out var properties))
                {
                    // A table with no matching entity is not something this test
                    // can judge; the fixture would still catch it.
                    continue;
                }

                foreach (var column in match.Groups["cols"].Value
                             .Split(',')
                             .Select(c => c.Trim().Trim('[', ']'))
                             .Where(c => c.Length > 0))
                {
                    if (!properties.Contains(column))
                    {
                        offenders.Add($"{Path.GetFileName(file)}: dbo.{table}.{column}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These content seeds name columns that no longer exist. A stale column "
            + "list fails the whole seed, and the seeds build the integration-test "
            + "database, so this breaks every API test at once: "
            + string.Join("; ", offenders.Distinct()));
    }

    // The entity's own name is the table's singular in this codebase (Sponsors ->
    // Sponsor), with a few already-plural names. Try both.
    private static bool TryFindEntity(
        IReadOnlyDictionary<string, HashSet<string>> entities,
        string table,
        out HashSet<string> properties)
    {
        if (entities.TryGetValue(table, out properties!))
        {
            return true;
        }
        if (table.EndsWith("ies", StringComparison.OrdinalIgnoreCase)
            && entities.TryGetValue(table[..^3] + "y", out properties!))
        {
            return true;
        }
        if (table.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && entities.TryGetValue(table[..^1], out properties!))
        {
            return true;
        }
        properties = null!;
        return false;
    }

    private static Dictionary<string, HashSet<string>> EntityPropertiesByTypeName()
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(SIMF.Domain.Files.StoredFile).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.Namespace is null)
            {
                continue;
            }
            var names = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            result[type.Name] = names;
        }
        return result;
    }

    private static IEnumerable<string> SeedFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot(), "docs", "migrations"), "*.sql", SearchOption.AllDirectories);

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
