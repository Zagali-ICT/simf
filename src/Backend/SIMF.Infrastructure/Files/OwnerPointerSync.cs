// Tests: SIMF.Api.Tests/AssetOwnerPointerTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Common.Enums;
using SIMF.Domain.Files;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Files;

/// <summary>Keeps an owning row's typed pointer in step with its file.
///
/// <para>A <see cref="StoredFile"/> and the row that owns it are linked twice, on
/// purpose. The file carries a polymorphic <c>OwnerEntityType</c>/<c>OwnerEntityId</c>
/// pair, which the serve path queries because it answers for every category —
/// including the ones whose owning row has no pointer column at all. The owning
/// row carries a typed <c>Guid? XFileId</c>, which is the reference a caller
/// already holding that row can follow without a second query, and the only one
/// of the two a foreign key can constrain.</para>
///
/// <para>Two links mean two chances to disagree, so the mapping between a
/// <see cref="FileService"/> and the column it feeds lives here and nowhere else,
/// and every path that changes which file is active for an owner goes through
/// these two methods — the asset pipeline's upload / link / deactivate / restore,
/// and the file store's own delete and force-delete. That last pair is the reason
/// this is not a private method on the asset service: a file deleted straight
/// through <c>DELETE /files/{id}</c> never reaches that service, and would
/// otherwise leave the owning row pointing at a dead file.</para>
///
/// <para>A service with no pointer column falls through to a no-op, which is the
/// common case: only five of the sixteen have one.</para></summary>
internal static class OwnerPointerSync
{
    /// <summary>Point the owner's row at <paramref name="fileId"/>.</summary>
    public static Task PointAtAsync(
        SimfAppDbContext dbContext, FileService service, Guid? ownerId, Guid fileId,
        CancellationToken cancellationToken) =>
        SetAsync(dbContext, service, ownerId, fileId, onlyWhenPointingAt: null, cancellationToken);

    /// <summary>Clear the owner's pointer, but only while it still names
    /// <paramref name="fileId"/>. The guard is what makes the call order-independent:
    /// replacing an asset points the row at the new file and then retires the old
    /// one, and an unguarded clear would undo the replacement it just made.</summary>
    public static Task ClearIfPointingAtAsync(
        SimfAppDbContext dbContext, FileService service, Guid? ownerId, Guid fileId,
        CancellationToken cancellationToken) =>
        SetAsync(dbContext, service, ownerId, null, onlyWhenPointingAt: fileId, cancellationToken);

    private static async Task SetAsync(
        SimfAppDbContext dbContext, FileService service, Guid? ownerId, Guid? fileId,
        Guid? onlyWhenPointingAt, CancellationToken cancellationToken)
    {
        if (ownerId is not { } owner || owner == Guid.Empty)
        {
            return;
        }

        switch (service)
        {
            case FileService.SponsorLogo:
            {
                var row = await dbContext.Sponsors
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LogoFileId, onlyWhenPointingAt)) { return; }
                row.LogoFileId = fileId;
                break;
            }
            case FileService.MediaPartnerLogo:
            {
                var row = await dbContext.MediaPartners
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LogoFileId, onlyWhenPointingAt)) { return; }
                row.LogoFileId = fileId;
                break;
            }
            case FileService.NewsImage:
            {
                var row = await dbContext.News
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.ImageFileId, onlyWhenPointingAt)) { return; }
                row.ImageFileId = fileId;
                break;
            }
            case FileService.ArchiveCover:
            {
                var row = await dbContext.ArchiveEditions
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.CoverImageFileId, onlyWhenPointingAt)) { return; }
                row.CoverImageFileId = fileId;
                break;
            }
            case FileService.Banner:
            {
                var row = await dbContext.Banners
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.ImageFileId, onlyWhenPointingAt)) { return; }
                row.ImageFileId = fileId;
                break;
            }
            default:
                return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool Matches(Guid? current, Guid? required) =>
        required is null || current == required;
}
