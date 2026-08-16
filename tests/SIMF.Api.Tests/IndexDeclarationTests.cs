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
// The name is what makes it detectable: a HasDatabaseName string in a
// configuration file is a promise that an index by that name exists. This test
// holds the source to that promise.
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

    private static readonly Regex DeclaredIndexName =
        new(@"HasDatabaseName\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

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

    private static IReadOnlyList<(string Name, string File)> DeclaredIndexNames()
    {
        var configurations = Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Persistence", "Configurations");

        return [.. Directory
            .EnumerateFiles(configurations, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => DeclaredIndexName
                .Matches(File.ReadAllText(path))
                .Select(match => (match.Groups[1].Value, Path.GetFileName(path))))];
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
