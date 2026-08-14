// Tests: SIMF.Api.Tests/PublicSpeakersTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Public, anonymous reads over the
/// <see cref="SIMF.Domain.Programme.Speaker"/> surface. Read-only sibling
/// of <see cref="AdminSpeakerService"/>: only active speakers are returned
/// (<c>IsActive</c>), ordered by <c>DisplayOrder</c>.
///
/// <para>The country name is read through the <c>Speaker.Country</c>
/// navigation in the same query (one LEFT JOIN), replacing the prior
/// dictionary-stitch that fetched the distinct country ids in a separate
/// round-trip for the list and a single-row lookup for the detail.</para>
///
/// <para>Privacy: the profile surfaces the social URLs only when the
/// speaker has opted into data-sharing (<c>AllowsDataSharing</c>); the
/// account link (<c>UserProfileId</c>) is never surfaced. Sessions are the
/// speaker's active sessions only (the inactive-session and inactive-
/// speaker rows are filtered out), ordered by start time.</para>
/// </summary>
internal sealed class PublicSpeakerService(SimfAppDbContext dbContext)
    : IPublicSpeakerService
{
    public async Task<PublicSpeakers> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Speakers
            .AsNoTracking()
            .Where(speaker => speaker.IsActive)
            .OrderBy(speaker => speaker.DisplayOrder)
            .ThenBy(speaker => speaker.Name)
            .Select(speaker => new
            {
                speaker.Id,
                speaker.Name,
                speaker.NameArabic,
                speaker.Rank,
                speaker.RankArabic,
                speaker.CountryId,
                // The country name comes through the nav in the same query
                // (was a separate dictionary-stitch round-trip).
                CountryNameEn = speaker.Country != null ? speaker.Country.Name : null,
                CountryNameAr = speaker.Country != null ? speaker.Country.NameArabic : null,
                speaker.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        // Which of these speakers have an active SpeakerPhoto asset (one
        // batched query; OwnerId is the speaker id, resolved cross-row with no FK
        // so it cannot fold into the main projection's join).
        var speakerIds = rows.Select(row => row.Id).ToList();
        // The photo now lives in the unified StoredFile store.
        var withPhotoAsset = (await dbContext.StoredFiles
            .AsNoTracking()
            .Where(file => file.Service == FileService.SpeakerPhoto
                && file.IsActive
                && file.OwnerEntityId != null
                && speakerIds.Contains(file.OwnerEntityId.Value))
            .Select(file => file.OwnerEntityId!.Value)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = rows
            .Select(row => new PublicSpeakerSummary(
                row.Id, row.Name, row.NameArabic, row.Rank, row.RankArabic,
                row.CountryId, row.CountryNameEn, row.CountryNameAr,
                null, row.DisplayOrder,
                withPhotoAsset.Contains(row.Id)))
            .ToList();

        return new PublicSpeakers(items);
    }

    public async Task<PublicSpeakerDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var speaker = await dbContext.Speakers
            .AsNoTracking()
            .Where(row => row.IsActive && row.Id == id)
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.NameArabic,
                row.Rank,
                row.RankArabic,
                row.CountryId,
                // Country name via the nav (was a separate single-row query).
                CountryNameEn = row.Country != null ? row.Country.Name : null,
                CountryNameAr = row.Country != null ? row.Country.NameArabic : null,
                row.Bio,
                row.BioArabic,
                row.Qualifications,
                row.QualificationsArabic,
                row.TrainingExperience,
                row.TrainingExperienceArabic,
                row.Awards,
                row.AwardsArabic,
                row.AllowsMeetingRequests,
                row.AllowsDataSharing,
                row.FacebookUrl,
                row.LinkedInUrl,
                row.XUrl,
                row.WebsiteUrl,
                row.DisplayOrder,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (speaker is null)
        {
            return null;
        }

        // The speaker's active sessions, via the SessionSpeaker join.
        // Only active sessions are surfaced; ordered by start time. The
        // hall is projected EN + AR so the client needs no second fetch.
        var sessions = await dbContext.SessionSpeakers
            .AsNoTracking()
            .Where(link => link.SpeakerId == id && link.Session!.IsActive)
            .OrderBy(link => link.Session!.Start)
            .ThenBy(link => link.Session!.Title)
            .Select(link => new PublicSpeakerSession(
                link.Session!.Id,
                link.Session!.Code,
                link.Session!.Title,
                link.Session!.TitleArabic,
                link.Session!.HallId,
                link.Session!.Hall!.Name,
                link.Session!.Hall!.NameArabic,
                link.Session!.Start,
                link.Session!.End))
            .ToListAsync(cancellationToken);

        // Privacy: social URLs are only published when the speaker has
        // consented to data-sharing; otherwise they stay admin-only.
        var publishSocial = speaker.AllowsDataSharing;

        return new PublicSpeakerDetail(
            speaker.Id,
            speaker.Name,
            speaker.NameArabic,
            speaker.Rank,
            speaker.RankArabic,
            speaker.CountryId,
            speaker.CountryNameEn,
            speaker.CountryNameAr,
            speaker.Bio,
            speaker.BioArabic,
            speaker.Qualifications,
            speaker.QualificationsArabic,
            speaker.TrainingExperience,
            speaker.TrainingExperienceArabic,
            speaker.Awards,
            speaker.AwardsArabic,
            speaker.AllowsMeetingRequests,
            speaker.AllowsDataSharing,
            publishSocial ? speaker.FacebookUrl : null,
            publishSocial ? speaker.LinkedInUrl : null,
            publishSocial ? speaker.XUrl : null,
            publishSocial ? speaker.WebsiteUrl : null,
            null,
            speaker.DisplayOrder,
            sessions);
    }
}
