using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>D-568 (Wave C S6) — resolves a speaker presentation's bytes from the
/// unified <c>StoredFile</c> store. <c>SpeakerPresentation.StoredFileName</c> is
/// repurposed as the bare-Guid pointer to the <c>StoredFile</c>; the bytes are read
/// (and decrypted — the presentation is Internal/encrypted-at-rest) via the storage
/// provider. Shared by the admin + public presentation services so the resolve is
/// written once. AES-GCM integrity is intrinsic (a tampered blob decrypts to null).</summary>
internal static class PresentationFileReader
{
    public static async Task<byte[]?> ReadBytesAsync(
        SimfAppDbContext db, IFileStorageProvider storage, string pointer,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(pointer, out var fileId)) { return null; }
        var locator = await db.StoredFiles.AsNoTracking()
            .Where(f => f.Id == fileId && f.IsActive && f.StorageKey != null)
            .Select(f => new { f.StorageKey, f.IsEncrypted })
            .FirstOrDefaultAsync(cancellationToken);
        if (locator is null) { return null; }
        return await storage.ReadAsync(locator.StorageKey!, locator.IsEncrypted, cancellationToken);
    }
}
