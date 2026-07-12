// Tests: SIMF.Api.Tests/SqlContentSeederTests.cs
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// D-747 — Development / Testing convenience runner that applies the by-hand
/// 2026 content-seed SQL files (<c>docs/migrations/2026/*.sql</c>) against
/// <c>SIMF_App</c> so a fresh dev or test database is not empty. The content
/// lane moved out of the C# seeders into these SQL files (D-718 / owner rule);
/// this runner is the ONLY thing that auto-applies them, and it runs ONLY in
/// Development and Testing. <b>Production never runs it</b> — production content
/// is curated and applied by hand (see <c>docs/migrations/2026/README.md</c>).
///
/// <para>Idempotent by virtue of the files themselves: every INSERT is guarded
/// by <c>IF NOT EXISTS</c>, so re-running only inserts what is missing.</para>
///
/// <para>The files have no <c>GO</c> batch separators, so each is executed as a
/// single command. The folder is located by walking up from the app base
/// directory; when it is not found (e.g. a published production deployment) the
/// runner logs and no-ops rather than throwing.</para>
/// </summary>
public sealed class SqlContentSeeder(
    SimfAppDbContext appDbContext,
    ILogger<SqlContentSeeder> logger)
{
    /// <summary>The event-roster content files, in dependency order
    /// (speakers before their photos; programme before nothing else here).
    /// This is the set applied in <b>Testing</b>: it deliberately EXCLUDES
    /// <c>SIMF_App_SeedGaps.sql</c>, whose delegation <c>UserProfiles</c> would
    /// skew the profile-count integration tests.</summary>
    public static readonly IReadOnlyList<string> RosterFiles = new[]
    {
        "SIMF_App_Speakers.sql",
        "SIMF_App_SpeakerPhotos.sql",
        "SIMF_App_Programme.sql",
        "SIMF_App_News.sql",
        "SIMF_App_Sponsors.sql",
        "SIMF_App_MediaPartners.sql",
        "SIMF_App_Archive.sql",
        "SIMF_App_Organization.sql",
    };

    /// <summary>The full dev picture: the roster plus the four "gap"
    /// collections (booths / delegations / FAQ / venue map). Programme runs
    /// before SeedGaps because SeedGaps references <c>Halls.Code = 'MAIN'</c>.
    /// Applied in <b>Development</b>.</summary>
    public static readonly IReadOnlyList<string> AllFiles =
        RosterFiles.Append("SIMF_App_SeedGaps.sql").ToArray();

    public async Task RunAsync(
        IReadOnlyList<string> fileNames, CancellationToken cancellationToken = default)
    {
        var folder = LocateSeedFolder();
        if (folder is null)
        {
            logger.LogWarning(
                "2026 content-seed folder (docs/migrations/2026) not found — SQL content seed skipped.");
            return;
        }

        // Execute each file over the raw ADO.NET connection rather than
        // EF's ExecuteSqlRaw — the latter treats `{n}` in the script as
        // parameter placeholders, which corrupts any file that legitimately
        // contains a brace. A DbCommand runs the batch verbatim.
        var connection = appDbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var fileName in fileNames)
            {
                var path = Path.Combine(folder, fileName);
                if (!File.Exists(path))
                {
                    logger.LogWarning("Content-seed file missing, skipped: {File}", fileName);
                    continue;
                }

                var sql = await File.ReadAllTextAsync(path, cancellationToken);
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.CommandTimeout = 120;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Content-seed file '{fileName}' failed to apply: {ex.Message}", ex);
                }
                logger.LogInformation("Content-seed applied: {File}", fileName);
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>Walk up from the app base directory to find the repo's
    /// <c>docs/migrations/2026</c> folder. Works in Development + Testing (both
    /// run from inside the repo tree); returns <c>null</c> when not found — a
    /// published production deployment, which must never auto-run the seed.</summary>
    private static string? LocateSeedFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "migrations", "2026");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
