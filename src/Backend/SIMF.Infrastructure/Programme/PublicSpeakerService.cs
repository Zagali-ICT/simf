// Tests: SIMF.Api.Tests/PublicSpeakersTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// D-199 (Mockup pages 19-20) — public, anonymous reads over the
/// <see cref="SIMF.Domain.Programme.Speaker"/> surface. Read-only sibling
/// of <see cref="AdminSpeakerService"/>: only active speakers are returned
/// (<c>IsActive</c>), ordered by <c>DisplayOrder</c>.
///
/// <para>Country names are resolved with the same batch-dictionary
/// approach <see cref="AdminSpeakerService"/> uses: <c>Speaker</c> carries
/// a <c>CountryId</c> FK but no <c>Country</c> navigation property, so the
/// projection cannot dot through to the country in SQL. The list does one
/// extra query for the distinct country ids on the page; the detail does a
/// single-row lookup.</para>
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
                speaker.CountryId,
                speaker.PhotoRelativePath,
                speaker.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        var countriesById = await ResolveCountriesAsync(
            rows.Where(row => row.CountryId.HasValue)
                .Select(row => row.CountryId!.Value),
            cancellationToken);

        // D-357 — which of these speakers have an active SpeakerPhoto asset (one
        // batched query; OwnerId is the speaker id, resolved cross-row with no FK).
        var speakerIds = rows.Select(row => row.Id).ToList();
        var withPhotoAsset = (await dbContext.Assets
            .AsNoTracking()
            .Where(asset => asset.Category == AssetCategory.SpeakerPhoto
                && asset.IsActive
                && speakerIds.Contains(asset.OwnerId))
            .Select(asset => asset.OwnerId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = rows
            .Select(row =>
            {
                string? en = null, ar = null;
                if (row.CountryId.HasValue
                    && countriesById.TryGetValue(row.CountryId.Value, out var country))
                {
                    en = country.Name;
                    ar = country.NameArabic;
                }
                return new PublicSpeakerSummary(
                    row.Id, row.Name, row.NameArabic, row.Rank,
                    row.CountryId, en, ar,
                    row.PhotoRelativePath, row.DisplayOrder,
                    withPhotoAsset.Contains(row.Id));
            })
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
                row.CountryId,
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
                row.PhotoRelativePath,
                row.DisplayOrder,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (speaker is null)
        {
            return null;
        }

        string? countryNameEn = null, countryNameAr = null;
        if (speaker.CountryId is { } countryId)
        {
            var country = await dbContext.Countries
                .AsNoTracking()
                .Where(c => c.Id == countryId)
                .Select(c => new { c.Name, c.NameArabic })
                .SingleOrDefaultAsync(cancellationToken);
            countryNameEn = country?.Name;
            countryNameAr = country?.NameArabic;
        }

        // The speaker's active sessions, via the SessionSpeaker join.
        // Only active sessions are surfaced; ordered by start time. The
        // hall is projected EN + AR so the client needs no second fetch.
        var sessions = await dbContext.SessionSpeakers
            .AsNoTracking()
            .Where(link => link.SpeakerId == id && link.Session!.IsActive)
            .OrderBy(link => link.Session!.StartUtc)
            .ThenBy(link => link.Session!.Title)
            .Select(link => new PublicSpeakerSession(
                link.Session!.Id,
                link.Session!.Code,
                link.Session!.Title,
                link.Session!.TitleArabic,
                link.Session!.HallId,
                link.Session!.Hall!.Name,
                link.Session!.Hall!.NameArabic,
                link.Session!.StartUtc,
                link.Session!.EndUtc))
            .ToListAsync(cancellationToken);

        // Privacy: social URLs are only published when the speaker has
        // consented to data-sharing; otherwise they stay admin-only.
        var publishSocial = speaker.AllowsDataSharing;

        return new PublicSpeakerDetail(
            speaker.Id,
            speaker.Name,
            speaker.NameArabic,
            speaker.Rank,
            speaker.CountryId,
            countryNameEn,
            countryNameAr,
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
            speaker.PhotoRelativePath,
            speaker.DisplayOrder,
            sessions);
    }

    private async Task<IReadOnlyDictionary<int, (string Name, string NameArabic)>>
        ResolveCountriesAsync(
            IEnumerable<int> countryIds, CancellationToken cancellationToken)
    {
        var ids = countryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, (string, string)>();
        }
        return await dbContext.Countries
            .AsNoTracking()
            .Where(country => ids.Contains(country.Id))
            .Select(country => new { country.Id, country.Name, country.NameArabic })
            .ToDictionaryAsync(
                country => country.Id,
                country => (country.Name, country.NameArabic),
                cancellationToken);
    }
}
