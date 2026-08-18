// Tests: this file IS the test — it pins the cipher-stamp half of
//        StoredFileService.RestoreBytesAsync, which nothing else covered.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Files.Abstractions;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>A restore re-seals the bytes under whatever KEK is active NOW, so the
/// row's cipher stamps have to move with them.
///
/// <para>Left stale, <c>StoredFile.KekVersion</c> names a key the blob on disk is
/// no longer wrapped under. Nothing fails at the time: the read still works,
/// because the version the reader needs is carried in the blob's own header. What
/// breaks is the rotation inventory the column exists for — it would count this
/// file as already re-wrapped, skip it, and the operator would retire the old key
/// with a file still depending on it.</para>
///
/// <para>The seeder is the caller that makes this real: it restores bytes for rows
/// whose blobs went missing, which is exactly the situation where the row's stamp
/// predates the current key. Both directions are asserted, because the guard is a
/// conditional save and a guard that always fires is a different bug from one that
/// never does.</para></summary>
[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class StoredFileRestoreStampTests : IClassFixture<SimfApiFactory>
{
    // SpeakerPresentation encrypts at rest and needs no owner id, so the upload is
    // a single in-process call with no profile to seed behind it.
    private const FileService EncryptingService = FileService.SpeakerPresentation;

    private static readonly Guid ActorId = Guid.Parse("00000000-0000-0000-0000-0000000000A3");

    private static readonly byte[] Pdf =
        "%PDF-1.4\n%bytes the row's SHA-256 is computed over\n"u8.ToArray();

    // Not a version any deployment mints: the active KEK defaults to 1 and a
    // rotation moves it by one, so a stamp this far out can only be the one the
    // test wrote.
    private const byte StaleKekVersion = 99;

    // Older than any run of the suite, so "unchanged" is provable rather than a
    // coincidence of two writes landing in the same clock tick.
    private static readonly DateTime Sentinel = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly SimfApiFactory _factory;

    public StoredFileRestoreStampTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Restore_refreshes_a_stale_KekVersion_stamp()
    {
        var id = await SeedEncryptedFileAsync();

        byte activeKekVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var seeded = await db.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == id);
            Assert.True(seeded.IsEncrypted);
            Assert.NotNull(seeded.KekVersion);
            activeKekVersion = seeded.KekVersion!.Value;

            await db.StoredFiles
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(f => f.KekVersion, StaleKekVersion));
        }

        // The SAME bytes: a restore puts back what the row already describes, and
        // the row's SHA-256 is not recomputed, so different content would fail the
        // integrity re-check on the next read rather than exercise the stamp.
        using (var scope = _factory.Services.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<IFileService>();
            Assert.True(await files.RestoreBytesAsync(id, Pdf, ActorId));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var restored = await db.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == id);
            Assert.Equal(activeKekVersion, restored.KekVersion);

            var files = scope.ServiceProvider.GetRequiredService<IFileService>();
            var content = await files.ReadContentAsync(id);
            Assert.NotNull(content);
            Assert.Equal(Pdf, content!.Content);
        }
    }

    [Fact]
    public async Task Restore_does_not_write_the_row_when_the_stamps_already_agree()
    {
        var id = await SeedEncryptedFileAsync();

        // AuditStampingSaveChangesInterceptor stamps UpdatedAt on every modified
        // BaseAuditEntity, so a sentinel that survives the call is proof no save
        // ran — a stronger claim than "the stamps look the same afterwards",
        // which an unconditional re-write would also satisfy.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            await db.StoredFiles
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(f => f.UpdatedAt, Sentinel));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<IFileService>();
            Assert.True(await files.RestoreBytesAsync(id, Pdf, ActorId));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var after = await db.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == id);
            Assert.Equal(Sentinel, after.UpdatedAt);
        }
    }

    private async Task<Guid> SeedEncryptedFileAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var result = await files.UploadAsync(new UploadFileCommand(
            EncryptingService, OwnerEntityId: null, Pdf,
            "restore-stamp.pdf", "application/pdf", ActorId, FailClosed: true));
        return result.Id;
    }
}
