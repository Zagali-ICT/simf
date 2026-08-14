using SIMF.Common.Enums;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>Seeds an externally hosted feed the way the application does: a
/// <c>StoredFile</c> row of source type <c>ExternalLink</c>, owned by the entity,
/// with the owning row pointing at it.
///
/// <para>A fixture cannot simply assign a URL any more, and it cannot invent a
/// Guid either — the pointer carries a real foreign key, so a fabricated id is
/// rejected by the database rather than quietly stored. That rejection is the
/// point of the key; this helper is how a test satisfies it.</para></summary>
internal static class FeedLinkSeed
{
    /// <summary>Adds the file row to <paramref name="db"/> and returns its id for
    /// the caller to store on the owning entity. Does not save: the caller's own
    /// <c>SaveChangesAsync</c> commits both halves together, which is also what
    /// keeps the foreign key satisfiable in either insertion order.</summary>
    public static Guid Add(
        SimfAppDbContext db,
        FileService service,
        Guid ownerId,
        string url,
        FileOwnerEntityType ownerType)
    {
        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            Service = service,
            SensitivityTier = FileSensitivityTier.Public,
            FileType = FileType.Video,
            SourceType = FileSourceType.ExternalLink,
            ExternalUrl = url,
            OwnerEntityType = ownerType,
            OwnerEntityId = ownerId,
            IsDeletable = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.StoredFiles.Add(file);
        return file.Id;
    }
}
