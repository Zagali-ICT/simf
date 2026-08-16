// A configuration can declare an index the model never builds, and nothing
// upstream notices.
//
// GateScanConfiguration declared two indexes over the identical property pair
// (UserProfileId, ScannedAt) - one plain history index, one filtered to allowed
// scans - using the UNNAMED HasIndex overload for both. That overload is keyed on
// the property set alone, so the second call did not declare a second index: it
// reconfigured the first, renaming IX_GateScan_UserProfile_ScannedAt to
// IX_GateScan_UserProfile_LastAllowed and adding its filter. The source read as
// two indexes, the migration built one, and the history index was gone. It
// compiled, the model snapshot agreed with the configuration, every test passed,
// and only a manual read of the generated CreateTable block found it.
//
// Two facts, because one of them is not enough.
//
// The first holds the source to its promises: a HasDatabaseName in a
// configuration is a claim that an index by that name exists, so every such name
// must be in the model. That catches GateScan's case, where a name went missing.
//
// It catches only that case. Of 216 HasIndex declarations here, nine carry a
// name, so a collapse between two UNNAMED calls leaves nothing for a name check
// to miss - no name, no promise, no failure. The second fact reads the source for
// the collapse itself: the same property set declared twice through the overload
// that is keyed on the property set. That one covers all 217.
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class IndexDeclarationTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public IndexDeclarationTests(SimfApiFactory factory) => _factory = factory;

    /// <summary>Either <c>HasDatabaseName("literal")</c> or
    /// <c>HasDatabaseName(SomeConst)</c>. The const form is not a stylistic
    /// variant to tolerate: the one index name in this codebase with runtime
    /// behaviour attached is written that way. `IX_ProfileIdentityDocuments_NumberHash`
    /// is matched by string at two call sites to turn a unique-key violation into
    /// a 409 rather than an uncaught 500, so a literal-only pattern would check
    /// the eight names that do not matter and skip the one that does.</summary>
    private static readonly Regex DeclaredIndexName =
        new(@"HasDatabaseName\(\s*(?:""(?<literal>[^""]+)""|(?<symbol>[A-Za-z_][\w.]*))\s*\)",
            RegexOptions.Compiled);

    [Fact]
    public void Every_index_name_a_configuration_declares_exists_in_the_model()
    {
        var built = BuiltIndexNames();

        var missing = DeclaredIndexNames()
            .Where(declared => !built.Contains(declared.Name))
            .Select(declared => $"  {declared.Name} ({declared.File})")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These index names are declared with HasDatabaseName in an EF "
            + "configuration but do not exist in either model, so the migration "
            + "will not build them. The usual cause is two UNNAMED HasIndex calls "
            + "over the same property set in one entity configuration: the second "
            + "reconfigures the first instead of adding an index. Give each the "
            + "named HasIndex(expression, name) overload.\n"
            + string.Join('\n', missing));
    }

    /// <summary>The unnamed <c>HasIndex</c> overload, over an anonymous property
    /// set or a single property. Unnamed is the whole point: the named overload
    /// declares a distinct index, this one is keyed on the property set.</summary>
    private static readonly Regex UnnamedCompositeIndex =
        new(@"HasIndex\(\s*(?<p>\w+)\s*=>\s*new\s*\{(?<props>[^}]*)\}\s*\)", RegexOptions.Compiled);

    private static readonly Regex UnnamedSingleIndex =
        new(@"HasIndex\(\s*(?<p>\w+)\s*=>\s*\k<p>\.(?<prop>\w+)\s*\)", RegexOptions.Compiled);

    private static readonly Regex ClassDeclaration =
        new(@"^\s*(?:internal|public|private)?\s*(?:sealed\s+)?class\s+(?<name>\w+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void No_configuration_declares_the_same_property_set_twice_unnamed()
    {
        // The name check above cannot see this one. Two UNNAMED HasIndex calls over
        // the same property set do not declare two indexes: the second reconfigures
        // the first, because that overload is keyed on the property set alone. When
        // neither call carries a HasDatabaseName there is no name to go missing, so
        // an index disappears leaving no trace in the model, the snapshot or any
        // assertion - which is exactly how GateScan lost its history index.
        //
        // Scoped PER CLASS, not per file: several configuration files hold one
        // class per entity, and the same property set in two different entities is
        // ordinary. A repo-wide sweep at file scope reports three such pairs, all
        // of them false.
        var configurations = Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Persistence", "Configurations");

        var collisions = new List<string>();

        foreach (var path in Directory.EnumerateFiles(configurations, "*.cs", SearchOption.AllDirectories))
        {
            foreach (var (className, body) in ClassBodies(File.ReadAllText(path)))
            {
                var declared = UnnamedCompositeIndex.Matches(body)
                    .Select(match => string.Join(
                        ",",
                        match.Groups["props"].Value
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(property => property.Split('.')[^1])
                            .OrderBy(property => property, StringComparer.Ordinal)))
                    .Concat(UnnamedSingleIndex.Matches(body).Select(match => match.Groups["prop"].Value));

                collisions.AddRange(declared
                    .GroupBy(properties => properties, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => $"  {Path.GetFileName(path)} / {className}: ({group.Key}) x{group.Count()}"));
            }
        }

        Assert.True(
            collisions.Count == 0,
            "A configuration declares the same property set more than once with the "
            + "UNNAMED HasIndex overload, so only the LAST of them exists: each call "
            + "after the first reconfigures the one before it rather than adding an "
            + "index. If you want two indexes over the same properties - typically one "
            + "plain and one filtered - each needs the named overload, "
            + "HasIndex(expression, \"IX_Name\").\n"
            + string.Join('\n', collisions));
    }

    /// <summary>Splits a configuration file into (class name, body) pairs so an
    /// index declaration is attributed to the entity that owns it.</summary>
    private static IEnumerable<(string Name, string Body)> ClassBodies(string source)
    {
        var declarations = ClassDeclaration.Matches(source);

        for (var i = 0; i < declarations.Count; i++)
        {
            var start = declarations[i].Index;
            var end = i + 1 < declarations.Count ? declarations[i + 1].Index : source.Length;
            yield return (declarations[i].Groups["name"].Value, source[start..end]);
        }
    }

    private static IReadOnlyList<(string Name, string File)> DeclaredIndexNames()
    {
        var configurations = Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Persistence", "Configurations");
        var constants = IndexNameConstants();

        return [.. Directory
            .EnumerateFiles(configurations, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => DeclaredIndexName
                .Matches(File.ReadAllText(path))
                .Select(match => (Resolve(match, constants, path), Path.GetFileName(path))))];
    }

    private static string Resolve(
        Match match, IReadOnlyDictionary<string, string> constants, string path)
    {
        if (match.Groups["literal"].Success)
        {
            return match.Groups["literal"].Value;
        }

        // The symbol may be written bare or qualified; the last segment is the
        // field name either way.
        var symbol = match.Groups["symbol"].Value.Split('.')[^1];

        Assert.True(
            constants.TryGetValue(symbol, out var value),
            $"{Path.GetFileName(path)} names its index with '{symbol}', which is not a "
            + "string constant on any type in SIMF.Infrastructure, so this test cannot "
            + "resolve it and would otherwise skip the declaration silently. Make it a "
            + "const string on the configuration type, or pass the name as a literal.");

        return value!;
    }

    /// <summary>Every compile-time string constant in the Infrastructure assembly,
    /// keyed by field name, so a name passed as a symbol resolves to the same
    /// value the compiler baked into the configuration.</summary>
    private static IReadOnlyDictionary<string, string> IndexNameConstants()
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in typeof(SimfAppDbContext).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field is { IsLiteral: true, IsInitOnly: false }
                    && field.FieldType == typeof(string)
                    && field.GetRawConstantValue() is string value)
                {
                    constants[field.Name] = value;
                }
            }
        }

        return constants;
    }

    private HashSet<string> BuiltIndexNames()
    {
        using var scope = _factory.Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        return [.. new[] { app.Model, identity.Model }
            .SelectMany(model => model.GetEntityTypes())
            .SelectMany(entity => entity.GetIndexes())
            .Select(index => index.GetDatabaseName())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)];
    }

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
