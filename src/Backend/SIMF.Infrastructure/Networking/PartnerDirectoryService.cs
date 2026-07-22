// Tests: SIMF.Api.Tests/PartnerDirectoryServiceTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Networking.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Contracts.Networking;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Networking;

/// <summary>
/// Build #13 — "Meet People Like You" partner directory. A deduped union of the
/// curated exhibition entities (Sponsors, Speakers, Booth companies) and the
/// opted-in "Other"-type user accounts. Normal / VIP visitors never appear: they
/// are not in the curated lists, and the account pool requires
/// <c>ProfileType.IsForVisitor == false</c>. The whole feature is gated by the CP
/// switch <c>OrganizationProfile.PartnerDirectoryEnabled</c> (off → empty).
///
/// <para>The three curated sections reuse the same public read services the
/// app's speakers / sponsors / booths screens use (<see cref="IPublicSpeakerService"/>,
/// <see cref="IPublicSponsorService"/>, <see cref="IPublicBoothService"/>) so the
/// name / logo / country resolution (Contact-first sponsors, exhibitor-first
/// booths, soft-delete filtering) lives once, in those services, rather than
/// being re-projected here.</para>
///
/// <para>Two-DB (D-157): the account pool comes from the Identity DB in its own
/// round-trip; there is no cross-DB JOIN. Logos follow the existing convention —
/// a relative path or the owning contact id, never an absolute URL (the client
/// builds the asset URL per kind).</para>
/// </summary>
internal sealed class PartnerDirectoryService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IPublicSpeakerService speakerService,
    IPublicSponsorService sponsorService,
    IPublicBoothService boothService) : IPartnerDirectoryService
{
    public async Task<PartnerDirectoryResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        // The CP switch. Fail-open (true) only if the seeded singleton is absent —
        // matches SiteSettingsService.
        var enabled = await appDbContext.OrganizationProfile.AsNoTracking()
            .Where(p => p.Id == OrganizationProfile.SingletonId)
            .Select(p => (bool?)p.PartnerDirectoryEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;
        if (!enabled)
        {
            return new PartnerDirectoryResponse(Array.Empty<PartnerDirectoryEntry>());
        }

        var entries = new List<PartnerDirectoryEntry>();

        // 1) Speakers — the same public list the /app/speakers screen shows (name,
        //    rank, photo, country). Tap → speaker profile.
        var speakers = await speakerService.ListAsync(cancellationToken);
        entries.AddRange(speakers.Items.Select(s => new PartnerDirectoryEntry(
            PartnerDirectoryKind.Speaker, s.Id, s.Name, s.NameArabic,
            s.Rank, s.RankArabic, s.PhotoRelativePath, null,
            s.CountryId, s.CountryNameEn, s.CountryNameAr)));

        // 2) Sponsors — the same public list (Contact-first name / logo / country,
        //    tagline as the subtitle), flattened out of the tier groups. Logo served
        //    by sponsor id (SponsorLogo). Tap → sponsor detail.
        var sponsors = await sponsorService.ListAsync(cancellationToken);
        entries.AddRange(sponsors.Groups
            .SelectMany(g => g.Sponsors)
            .Select(s => new PartnerDirectoryEntry(
                PartnerDirectoryKind.Sponsor, s.Id, s.NameEn, s.NameAr,
                s.Tagline, s.TaglineArabic, s.LogoRelativePath, null,
                s.CountryId, s.CountryNameEn, s.CountryNameAr)));

        // 3) Booth companies — the same public booth list, keyed by booth id (the
        //    exhibitor-detail route takes a boothId). Company name from the linked
        //    Exhibitor, sector as subtitle, logo via the exhibitor's Contact id
        //    (CompanyLogo), country via Exhibitor → Contact. Tap → exhibitor detail.
        var booths = await boothService.ListAsync(cancellationToken);
        entries.AddRange(booths.Select(b => new PartnerDirectoryEntry(
            PartnerDirectoryKind.Booth, b.Id, b.ExhibitorName ?? string.Empty,
            b.ExhibitorNameArabic ?? string.Empty, b.Sector, b.SectorArabic,
            null, b.ExhibitorContactId, b.CountryId, b.CountryName, b.CountryNameArabic)));

        // 4) Opted-in "Other"-type accounts. Pool = Approved, non-Admin users
        //    (Identity DB, shared with the recommender via WhereApprovedNonAdmin).
        //    Include only non-visitor profile types that opted in; this is what
        //    keeps Normal / VIP out. De-dup: a person already curated as a Speaker
        //    (linked UserProfileId) is dropped — the curated entity wins.
        var approvedIds = await identityDbContext.Users.AsNoTracking()
            .WhereApprovedNonAdmin()
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (approvedIds.Count > 0)
        {
            // The user-profile ids already curated as active Speakers. Read as a
            // minimal projection here (not from the public speaker DTO, which
            // deliberately never surfaces the UserProfileId account link) purely
            // for the de-dup below.
            var curated = (await appDbContext.Speakers.AsNoTracking()
                .Where(s => s.IsActive && s.UserProfileId != null)
                .Select(s => s.UserProfileId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var people = await appDbContext.UserProfiles.AsNoTracking()
                .Where(p => approvedIds.Contains(p.UserId)
                    && p.ShowInMeetLikeYou
                    && p.ProfileType != null && !p.ProfileType.IsForVisitor)
                .OrderBy(p => p.NameArabic).ThenBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.NameArabic,
                    p.JobTitle,
                    p.JobTitleArabic,
                })
                .ToListAsync(cancellationToken);

            entries.AddRange(people
                .Where(p => !curated.Contains(p.Id))
                .Select(p => new PartnerDirectoryEntry(
                    PartnerDirectoryKind.Person, p.Id, p.Name, p.NameArabic,
                    p.JobTitle, p.JobTitleArabic, null, null, null, null, null)));
        }

        return new PartnerDirectoryResponse(entries);
    }
}
