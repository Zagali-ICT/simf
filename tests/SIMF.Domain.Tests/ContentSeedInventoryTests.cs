using System.Text.RegularExpressions;
using Xunit;

namespace SIMF.Domain.Tests;

/// <summary>
/// One location, one runner, and no file that belongs to neither.
///
/// <para>Owner rule, 2026-08-30: <em>"All sql seed has one script to run and all
/// seed in one location."</em> The location half already held. The runner half
/// did not, and could not be checked: the content seeds are named in THREE
/// hand-maintained lists - <c>SqlContentSeeder.AllFiles</c> in C# (the API test
/// fixture runs <c>RosterFiles</c>, the subset it is built from); the <c>:r</c>
/// includes in <c>Run_All_App_Seeds.sql</c>,
/// which an operator runs from SSMS; and the <c>$seeds</c> array in
/// <c>Run-AppSeeds.ps1</c>, the terminal runner the README calls preferred -
/// with nothing detecting disagreement between them.</para>
///
/// <para>They agree today. That is the moment to pin it: two lists of the same
/// files that must stay identical, with no mechanism, is the shape that produced
/// three <c>set-env</c> scripts silently disagreeing on a shared namespace
/// (D-859) and an E2E index claiming 3187 scenarios when the directory held
/// 3200. A seed present in one list and absent from the other does not fail
/// loudly - it produces a dev database that renders and a production database
/// that is missing a page of content, or the reverse, discovered at the venue.
/// </para>
///
/// <para><b>A file in neither list is the third failure, and the quiet one.</b>
/// It looks like a seed, sits beside the seeds, and never runs anywhere. The
/// exclusions below are therefore named individually with a reason rather than
/// pattern-matched, so adding a <c>.sql</c> here forces a decision about which
/// lane it belongs to instead of letting it default to none.</para>
/// </summary>
public sealed class ContentSeedInventoryTests
{
    /// <summary>The runner itself, which is not a seed.</summary>
    private const string RunnerFile = "Run_All_App_Seeds.sql";

    /// <summary>Files that are deliberately in NEITHER list, each with the reason
    /// it is not a content seed. Every entry is asserted to still exist, so this
    /// list can only shrink - a name left here after its file is deleted fails
    /// the build rather than rotting.
    ///
    /// <para>The three hotfixes patch a RUNNING database toward a newer model.
    /// A fresh database built from the current <c>InitialCreate</c> gets all
    /// three from the model itself, so enlisting them in the runner would make a
    /// clean install execute DDL the migration has already applied - which is
    /// exactly the collision that stops the API booting.</para></summary>
    private static readonly Dictionary<string, string> NotSeeds = new()
    {
        ["SIMF_App_RegistrationReferenceSequence_Hotfix.sql"] =
            "prod-only DDL: creates a sequence the model already declares "
            + "(SimfAppDbContext), so a fresh database needs nothing from it.",
        ["SIMF_App_D944_OrganisationOther_Hotfix.sql"] =
            "prod-only DDL: adds UserProfile.OrganisationOther and the seeded "
            + "'Other' row, both of which the model now carries.",
        ["SIMF_App_D945_DropIdentityDocumentUniqueIndex_Hotfix.sql"] =
            "prod-only DDL: drops an index the model no longer declares, so a "
            + "fresh database never creates it in the first place.",
        ["SIMF_App_AssistancePromptGrounding.sql"] =
            "an UPDATE, not a seed: re-points an already-seeded AI prompt at the "
            + "grounded template. Updates zero rows on a fresh database.",
        ["SIMF_App_AssistancePromptHistory.sql"] =
            "an UPDATE, not a seed: the history-carrying twin of the above.",
    };

    /// <summary>The PowerShell runner, which is the PREFERRED production route
    /// and a third hand-maintained copy of the same list.</summary>
    private const string PowerShellRunnerFile = "Run-AppSeeds.ps1";

    /// <summary>The <c>:r</c> includes, in order.</summary>
    private static readonly Regex RunnerInclude =
        new(@":r\s+\$\(MigrationDir\)\\(?<file>[A-Za-z0-9_]+\.sql)", RegexOptions.Compiled);

    /// <summary>The quoted entries of the PowerShell runner's <c>$seeds</c>
    /// array. Read as text for the same reason the C# list is: the test cannot
    /// execute PowerShell, and matching the literal filenames is the whole
    /// question.</summary>
    private static readonly Regex PowerShellSeedEntry =
        new("'(?<file>[A-Za-z0-9_]+\\.sql)'", RegexOptions.Compiled);

    /// <summary>The C# list. Nothing drives it at run time any more - the
    /// integration fixture builds its database from the roster subset of it.</summary>
    private static readonly Regex SeederEntry =
        new("\"(?<file>[A-Za-z0-9_]+\\.sql)\"", RegexOptions.Compiled);

    [Fact]
    public void Every_sql_file_is_either_a_content_seed_or_a_named_exception()
    {
        var runner = RunnerFiles();
        var unaccounted = SeedDirectory()
            .Select(path => Path.GetFileName(path)!)
            .Where(name => name != RunnerFile)
            .Where(name => !runner.Contains(name) && !NotSeeds.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            "A .sql file sits in docs/migrations/2026 and is in NEITHER the runner "
            + "nor the named-exception list, so it runs nowhere and nothing says "
            + "why. Add it to Run_All_App_Seeds.sql (and SqlContentSeeder.AllFiles) "
            + "if it is a content seed, or to NotSeeds with the reason it is not.\n"
            + string.Join('\n', unaccounted.Select(name => "  " + name)));
    }

    [Fact]
    public void The_runner_and_the_csharp_list_name_the_same_files()
    {
        // The runner is what production executes by hand; the C# list is what the
        // integration fixture builds its database from. A file in one and not the
        // other means the suite proves a page works against content production
        // never receives - discovered as "that page is empty on the server" long
        // after the change that caused it.
        var runner = RunnerFiles();
        var seeder = SeederFiles();

        var onlyInRunner = runner.Except(seeder).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var onlyInSeeder = seeder.Except(runner).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(
            onlyInRunner.Count == 0 && onlyInSeeder.Count == 0,
            "The two seed lists disagree. Run_All_App_Seeds.sql is what an "
            + "operator runs in production; SqlContentSeeder.AllFiles is what the "
            + "integration fixture is built from. They must name the same files.\n"
            + "  only in the runner: " + Describe(onlyInRunner) + "\n"
            + "  only in the seeder: " + Describe(onlyInSeeder));

        static string Describe(List<string> names) =>
            names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    [Fact]
    public void The_powershell_runner_names_the_same_files_as_the_sql_runner()
    {
        // There is a THIRD copy of this list, and it is the one production is
        // told to use: Run-AppSeeds.ps1 is documented as preferred over the
        // SSMS runner, so a seed present in the .sql runner and absent here does
        // not get run on the server at all - while every check that existed
        // before this one passed. That is not hypothetical: the 2026-08-30 move
        // of the lookups, content blocks and AI prompts out of IdentitySeeder
        // added three files, and the .sql runner and the C# list were both
        // updated by a test that could see them while this file was not covered
        // by anything.
        var sqlRunner = RunnerFiles();
        var powerShell = PowerShellSeedEntry
            .Matches(File.ReadAllText(Path.Combine(SeedFolder(), PowerShellRunnerFile)))
            .Select(match => match.Groups["file"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var onlyInSql = sqlRunner.Except(powerShell).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var onlyInPowerShell = powerShell.Except(sqlRunner).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(
            onlyInSql.Count == 0 && onlyInPowerShell.Count == 0,
            "Run-AppSeeds.ps1 and Run_All_App_Seeds.sql disagree. Both run the "
            + "content seeds in production - the first from a terminal or a deploy "
            + "script, the second from SSMS - so they must name the same files.\n"
            + "  only in Run_All_App_Seeds.sql: " + Describe(onlyInSql) + "\n"
            + "  only in Run-AppSeeds.ps1: " + Describe(onlyInPowerShell));

        static string Describe(List<string> names) =>
            names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    [Fact]
    public void A_named_exception_that_no_longer_exists_is_deleted()
    {
        // The list only ever shrinks. Three of these five stop existing the next
        // time both migrations are regenerated clean, and a name left behind
        // would quietly excuse a file nobody can find.
        var onDisk = SeedDirectory()
            .Select(path => Path.GetFileName(path)!)
            .ToHashSet(StringComparer.Ordinal);
        var stale = NotSeeds.Keys
            .Where(name => !onDisk.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            stale.Count == 0,
            "ContentSeedInventoryTests.NotSeeds excuses a file that is no longer "
            + "there. Remove the entry.\n"
            + string.Join('\n', stale.Select(name => "  " + name)));
    }

    private static HashSet<string> RunnerFiles() =>
        RunnerInclude
            .Matches(File.ReadAllText(Path.Combine(SeedFolder(), RunnerFile)))
            .Select(match => match.Groups["file"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> SeederFiles()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Backend", "SIMF.Infrastructure", "Seeding", "SqlContentSeeder.cs"));

        // Read the source rather than referencing the type: SIMF.Domain.Tests
        // deliberately does not depend on Infrastructure, and adding that
        // reference to reach one string array would be a worse trade than a
        // regex over a file this test already knows the shape of.
        return SeederEntry.Matches(source)
            .Select(match => match.Groups["file"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> SeedDirectory() =>
        Directory.EnumerateFiles(SeedFolder(), "*.sql", SearchOption.TopDirectoryOnly);

    private static string SeedFolder() =>
        Path.Combine(RepoRoot(), "docs", "migrations", "2026");

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
