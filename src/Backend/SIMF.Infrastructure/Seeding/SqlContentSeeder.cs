// Tests: SIMF.Api.Tests/SqlContentSeederTests.cs
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Files.Abstractions;
using SIMF.Infrastructure.Persistence;
// SIMF.Common.Enums is aliased, not imported: its ConnectionState collides with
// System.Data.ConnectionState, which this seeder uses on the raw ADO.NET connection.
using FileService = SIMF.Common.Enums.FileService;
using FileSourceType = SIMF.Common.Enums.FileSourceType;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// Development / Testing convenience runner that applies the by-hand
/// 2026 content-seed SQL files (<c>docs/migrations/2026/*.sql</c>) against
/// <c>SIMF_App</c> so a fresh dev or test database is not empty. The content
/// lane moved out of the C# seeders into these SQL files;
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
///
/// <para><b>Companion file bytes.</b> A content file that seeds
/// <c>StoredFile</c> rows (today only <c>SIMF_App_SpeakerPhotos.sql</c>) ships its
/// bytes as a deployable folder next to it, per the file-asset pattern. Production
/// copies that folder into <c>FileStorage:RootPath</c> by hand; nothing did so in
/// Development / Testing, so every seeded row pointed at a storage key with no
/// bytes and the image 404'd behind the UI's placeholder. <see cref="RunAsync"/>
/// now materialises those bytes through <see cref="IFileService"/> after
/// the SQL is applied, and <b>deactivates</b> any seeded row whose bytes cannot be
/// found — so a seeded asset reference either resolves to real bytes or is gone and
/// the surface shows its proper empty state. Idempotent: a row whose bytes are
/// already on disk is left untouched.</para>
/// </summary>
public sealed class SqlContentSeeder(
    SimfAppDbContext appDbContext,
    IFileService fileService,
    ILogger<SqlContentSeeder> logger)
{
    /// <summary>The seeder actor the content SQL stamps on every row it
    /// inserts (<c>@sys</c> in <c>docs/migrations/2026/*.sql</c>). The byte
    /// materialisation only ever touches rows carrying this <c>CreatedBy</c>, so a
    /// real admin upload is never inspected or deactivated.</summary>
    private static readonly Guid SeederActorUserId = Guid.Empty;

    /// <summary>Content file → (the file service whose rows it seeds, the
    /// repo folder holding those rows' bytes, relative to
    /// <c>docs/migrations/2026</c>). The byte file name is the storage key's leaf,
    /// so the folder layout mirrors the on-disk store exactly.</summary>
    private static readonly IReadOnlyDictionary<string, (FileService Service, string SourceFolder)>
        CompanionFileBytes = new Dictionary<string, (FileService, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SIMF_App_SpeakerPhotos.sql"] =
                (FileService.SpeakerPhoto, Path.Combine("speaker-photos", "speakerphoto")),
        };

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

        // The SQL only wrote the StoredFile ROWS; put their bytes on disk
        // (and retire any row whose bytes are missing) so no seeded asset reference
        // is left pointing at nothing.
        foreach (var fileName in fileNames)
        {
            if (!CompanionFileBytes.TryGetValue(fileName, out var companion)) { continue; }
            await MaterialiseSeededFileBytesAsync(
                Path.Combine(folder, companion.SourceFolder), companion.Service, cancellationToken);
        }
    }

    /// <summary>Copies the repo-shipped bytes of every seeded
    /// <paramref name="service"/> row into the file-storage root, so
    /// <c>AssetService.ResolveAsync</c> streams a real image instead of 404-ing
    /// behind the UI placeholder. Writes through <see cref="IFileService"/>, which
    /// rebuilds the SAME <c>{service}/{id:N}{ext}</c> key the SQL recorded, so the row
    /// and the blob cannot drift, and audits the write. A row whose bytes are already on disk is
    /// skipped (idempotent); a row with no source bytes is <b>deactivated</b> rather
    /// than left as a broken reference. Only rows stamped with
    /// <see cref="SeederActorUserId"/> are considered.</summary>
    private async Task MaterialiseSeededFileBytesAsync(
        string sourceFolder, FileService service, CancellationToken cancellationToken)
    {
        var rows = await appDbContext.StoredFiles
            .Where(file => file.Service == service
                && file.IsActive
                && file.SourceType == FileSourceType.Upload
                && file.CreatedBy == SeederActorUserId
                && file.StorageKey != null)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) { return; }

        var written = 0;
        var retired = 0;
        foreach (var row in rows)
        {
            var storageKey = row.StorageKey!;
            // A full read, not a cheap existence probe: a blob that is present but
            // unreadable (a corrupt or truncated cipher) must be rewritten, and an
            // existence check would skip it.
            if (await fileService.ReadContentAsync(row.Id, cancellationToken) is not null)
            {
                continue; // bytes already in place — idempotent skip.
            }

            var leaf = Path.GetFileName(storageKey.Replace('/', Path.DirectorySeparatorChar));
            var sourcePath = Path.Combine(sourceFolder, leaf);
            if (!File.Exists(sourcePath))
            {
                // No bytes anywhere: retire the row so the surface renders its empty
                // state instead of a broken image.
                row.Deactivate();
                retired++;
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
            if (await fileService.RestoreBytesAsync(row.Id, bytes, SeederActorUserId, cancellationToken))
            {
                written++;
            }
        }

        if (retired > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Content-seed file bytes for {Service}: {Written} written, {Retired} retired (no source bytes), "
            + "{Total} row(s) inspected.",
            service, written, retired, rows.Count);
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
