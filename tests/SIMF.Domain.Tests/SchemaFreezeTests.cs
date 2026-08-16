// Tests: D-895 — the D-110 schema freeze, re-instated 2026-08-14.
//
// The freeze had been prose in CLAUDE.md since 2026-05-26 and nothing anywhere
// enforced it: no test, no `tool/conventions` rule, nothing referencing
// InitialCreate or a migration count. Six lifts (D-186, D-199, D-211, D-217,
// D-219, D-881) accumulated on top of it over three months, and the baseline text
// was never corrected as they landed — by the end it described columns that had
// been renamed, one that was never built, and an EditionId that does not exist.
//
// That is the argument for a test rather than a paragraph, and it is CLAUDE.md's
// own rule: "pin the surface with a test, not a comment". A prose freeze cannot
// detect the seventh lift; this can.
using Xunit;

namespace SIMF.Domain.Tests;

public sealed class SchemaFreezeTests
{
    private const string MigrationsRoot =
        "src/Backend/SIMF.Infrastructure/Persistence/Migrations";

    public static TheoryData<string> Contexts() => new() { "App", "Identity" };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Each_context_has_exactly_one_InitialCreate(string context)
    {
        var folder = Path.Combine(
            RepoRoot(), MigrationsRoot.Replace('/', Path.DirectorySeparatorChar), context);

        Assert.True(Directory.Exists(folder), $"Migrations folder missing: {folder}");

        // *.Designer.cs is the same migration's designer half, not a second
        // migration, so it is excluded rather than counted.
        var migrations = Directory
            .EnumerateFiles(folder, "*.cs")
            .Select(Path.GetFileName)
            .Where(name => name is not null
                && !name.EndsWith(".Designer.cs", StringComparison.Ordinal)
                && !name.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            migrations.Count == 1,
            $"The D-110 freeze (re-instated by D-895) allows exactly ONE migration "
            + $"per context, and {context} now has {migrations.Count}: "
            + string.Join(", ", migrations)
            + ". A schema change needs a NEW, NAMED lift argued for on its own — "
            + "D-219's open-ended 'further audit-surfaced additions' was terminated "
            + "by D-895. The one sanctioned exception is the per-entity image "
            + "pipeline (Remaining-Work-Register 2.1), which regenerates the App "
            + "migration rather than adding a second one, so it does not trip this.");

        Assert.EndsWith("_InitialCreate.cs", migrations[0], StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void The_InitialCreate_id_is_pinned(string context)
    {
        // The rule above catches two migrations, but only once they are already on
        // main - and that is exactly how it went wrong, twice on 2026-08-16.
        //
        // `dotnet ef migrations add` stamps a fresh UTC timestamp, so two branches
        // that both regenerate InitialCreate produce two DIFFERENT paths. Git has
        // nothing to conflict on, the pull request reports mergeStatus "succeeded",
        // and both files land. Neither pull request is individually wrong, so no
        // gate on either one can see it; the breakage exists only in the state
        // after both merge, and by then main no longer compiles.
        //
        // Pinning the id makes the second branch write the SAME path as the first,
        // which git raises as an ordinary content conflict, resolved by running
        // tools/migrations/Regenerate-Migration.ps1 once on the merged model. That
        // is the only correct resolution in any case: a schema built by taking one
        // side of a merge is a schema nobody generated.
        var folder = Path.Combine(
            RepoRoot(), MigrationsRoot.Replace('/', Path.DirectorySeparatorChar), context);

        var migration = Directory
            .EnumerateFiles(folder, "*_InitialCreate.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Single(name => !name!.EndsWith(".Designer", StringComparison.Ordinal))!;

        Assert.True(
            migration == PinnedMigrationId,
            $"The {context} migration is '{migration}' but must be "
            + $"'{PinnedMigrationId}'. Regenerate it with "
            + "tools/migrations/Regenerate-Migration.ps1, which re-pins the id; "
            + "a raw 'dotnet ef migrations add' stamps a timestamp, and a "
            + "timestamped id is what let two regenerated migrations merge into "
            + "main side by side without conflicting.");

        // The filename is convention; this attribute is the id EF actually reads
        // and writes to __EFMigrationsHistory. They drift only if someone renames
        // the file by hand, which is precisely when it matters.
        var designer = File.ReadAllText(
            Path.Combine(folder, $"{PinnedMigrationId}.Designer.cs"));

        Assert.Contains($"[Migration(\"{PinnedMigrationId}\")]", designer, StringComparison.Ordinal);
    }

    // Not a timestamp, and deliberately not one: nothing about it invites a reader
    // to think it records when the schema was written. It sorts before any real
    // timestamp, so a future delta migration still applies after it.
    private const string PinnedMigrationId = "00000000000000_InitialCreate";

    [Fact]
    public void The_frozen_enum_surface_is_still_where_the_freeze_says_it_is()
    {
        // D-110 froze "every enum in src/Shared/SIMF.Common/Enums/" against rename
        // and reorder. Moving the directory would silently empty that rule, so its
        // location is asserted rather than assumed. The values themselves are not
        // pinned here: additive values are allowed, and pinning each one would fail
        // on exactly the change the freeze permits.
        var enums = Path.Combine(
            RepoRoot(), "src", "Shared", "SIMF.Common", "Enums");

        Assert.True(
            Directory.Exists(enums),
            $"The frozen enum surface is not at {enums}. CLAUDE.md's freeze names "
            + "that path; if the enums moved, the freeze text must move with them.");

        Assert.NotEmpty(Directory.EnumerateFiles(enums, "*.cs"));
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
