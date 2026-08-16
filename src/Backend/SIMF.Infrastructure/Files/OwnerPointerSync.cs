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
/// <para>A service with no pointer column falls through to a no-op. That used to be
/// the common case; Speaker, Booth, Exhibitor and ProgrammeDay gained columns so
/// their files stopped being reachable only through the polymorphic pair, which no
/// foreign key can constrain.</para></summary>
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
            case FileService.OrganizationLogo:
            {
                var row = await dbContext.OrganizationProfile
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LogoFileId, onlyWhenPointingAt)) { return; }
                row.LogoFileId = fileId;
                break;
            }
            case FileService.SpeakerPhoto:
            {
                var row = await dbContext.Speakers
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.PhotoFileId, onlyWhenPointingAt)) { return; }
                row.PhotoFileId = fileId;
                break;
            }
            case FileService.BoothLogo:
            {
                var row = await dbContext.Booths
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LogoFileId, onlyWhenPointingAt)) { return; }
                row.LogoFileId = fileId;
                break;
            }
            case FileService.ExhibitorLogo:
            {
                var row = await dbContext.Exhibitors
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LogoFileId, onlyWhenPointingAt)) { return; }
                row.LogoFileId = fileId;
                break;
            }
            case FileService.ProgrammeDayImage:
            {
                var row = await dbContext.ProgrammeDays
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.ImageFileId, onlyWhenPointingAt)) { return; }
                row.ImageFileId = fileId;
                break;
            }
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
            case FileService.SessionLiveStream:
            {
                var row = await dbContext.Sessions
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LiveStreamFileId, onlyWhenPointingAt)) { return; }
                row.LiveStreamFileId = fileId;
                break;
            }
            case FileService.SessionSignLanguage:
            {
                var row = await dbContext.Sessions
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LiveSignLanguageFileId, onlyWhenPointingAt)) { return; }
                row.LiveSignLanguageFileId = fileId;
                break;
            }
            case FileService.SessionSummaryVideo:
            {
                // Owned by the SESSION, not the summary row: a session has at most
                // one set of minutes, and keying on the session means the link can
                // be set before the summary exists without a second identifier.
                var row = await dbContext.SessionSummaries
                    .FirstOrDefaultAsync(x => x.SessionId == owner, cancellationToken);
                if (row is null || !Matches(row.SummaryVideoFileId, onlyWhenPointingAt)) { return; }
                row.SummaryVideoFileId = fileId;
                break;
            }
            case FileService.MediaGalleryVideo:
            {
                var row = await dbContext.MediaItems
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.VideoFileId, onlyWhenPointingAt)) { return; }
                row.VideoFileId = fileId;
                break;
            }
            case FileService.OrganizationLiveStream:
            {
                var row = await dbContext.OrganizationProfile
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.LiveStreamFileId, onlyWhenPointingAt)) { return; }
                row.LiveStreamFileId = fileId;
                break;
            }
            case FileService.ArchivePastSpeakerPhoto:
            {
                var row = await dbContext.ArchivePastSpeakers
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.PhotoFileId, onlyWhenPointingAt)) { return; }
                row.PhotoFileId = fileId;
                break;
            }
            case FileService.ArchiveGalleryImage:
            case FileService.ArchiveGalleryVideo:
            {
                // One column, two services: a gallery row holds either an
                // uploaded still or a link to a film, never both, and Kind says
                // which. Both must land here or the row would point at nothing.
                var row = await dbContext.ArchiveMediaItems
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.MediaFileId, onlyWhenPointingAt)) { return; }
                row.MediaFileId = fileId;
                break;
            }
            case FileService.OrganizationHeroVideo:
            {
                // The one service here that can be either bytes or a link: an
                // uploaded hero video and a pasted one now write the same column.
                var row = await dbContext.OrganizationProfile
                    .FirstOrDefaultAsync(x => x.Id == owner, cancellationToken);
                if (row is null || !Matches(row.BackgroundVideoFileId, onlyWhenPointingAt)) { return; }
                row.BackgroundVideoFileId = fileId;
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
