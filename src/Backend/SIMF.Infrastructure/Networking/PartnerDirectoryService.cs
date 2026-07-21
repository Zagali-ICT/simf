// Tests: SIMF.Api.Tests/PartnerDirectoryServiceTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Networking.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Networking;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Networking;

/// <summary>
/// Build #13 — "Meet People Like You" partner directory. A deduped union of the
/// curated exhibition entities (Sponsors, Speakers, Booth companies) and the
/// opted-in "Other"-type user accounts. Normal / VIP visitors never appear: they
/// are not in the curated tables, and the account pool requires
/// <c>ProfileType.IsForVisitor == false</c>. The whole feature is gated by the CP
/// switch <c>OrganizationProfile.PartnerDirectoryEnabled</c> (off → empty).
///
/// <para>Two-DB (D-157): the account pool comes from the Identity DB in its own
/// round-trip; there is no cross-DB JOIN. Small, cached-scale lists (like the
/// public sponsor / speaker / booth reads) are combined in memory. Logos follow
/// the existing convention — a relative path or the owning contact id, never an
/// absolute URL (the client builds the asset URL per kind).</para>
/// </summary>
internal sealed class PartnerDirectoryService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext) : IPartnerDirectoryService
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

        // 1) Speakers — the same public summary the /app/speakers list already
        //    shows (name, rank, photo, country). Tap → speaker profile.
        var speakers = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.UserProfileId,
                s.Name,
                s.NameArabic,
                s.Rank,
                s.RankArabic,
                s.PhotoRelativePath,
                s.CountryId,
                CountryName = s.Country != null ? s.Country.Name : null,
                CountryNameArabic = s.Country != null ? s.Country.NameArabic : null,
            })
            .ToListAsync(cancellationToken);
        entries.AddRange(speakers.Select(s => new PartnerDirectoryEntry(
            PartnerDirectoryKind.Speaker, s.Id, s.Name, s.NameArabic,
            s.Rank, s.RankArabic, s.PhotoRelativePath, null,
            s.CountryId, s.CountryName, s.CountryNameArabic)));

        // 2) Sponsors — shown by (Contact-first) company name; tagline as the
        //    subtitle. Logo served by sponsor id (SponsorLogo). Tap → sponsor detail.
        var sponsors = await appDbContext.Sponsors.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.NameArabic)
            .Select(s => new
            {
                s.Id,
                // Contact-first, but only when the linked Contact is active (a
                // soft-deleted Contact falls back to the sponsor's own columns) —
                // matches PublicSponsorService.
                Name = s.Contact != null && s.Contact.IsActive && s.Contact.Name != ""
                    ? s.Contact.Name : s.Name,
                NameArabic = s.Contact != null && s.Contact.IsActive && s.Contact.NameArabic != ""
                    ? s.Contact.NameArabic : s.NameArabic,
                s.Tagline,
                s.TaglineArabic,
                Logo = s.Contact != null && s.Contact.IsActive && s.Contact.LogoRelativePath != null
                    ? s.Contact.LogoRelativePath : s.LogoRelativePath,
            })
            .ToListAsync(cancellationToken);
        entries.AddRange(sponsors.Select(s => new PartnerDirectoryEntry(
            PartnerDirectoryKind.Sponsor, s.Id, s.Name ?? string.Empty, s.NameArabic ?? string.Empty,
            s.Tagline, s.TaglineArabic, s.Logo, null, null, null, null)));

        // 3) Booth companies — the exhibition companies, keyed by booth id (the
        //    exhibitor-detail route takes a boothId). Company name from the linked
        //    Exhibitor (legacy free-text fallback), sector as subtitle, logo via the
        //    exhibitor's Contact id (CompanyLogo), country via Exhibitor→Contact.
        //    A soft-deleted exhibitor hides its booths (matches PublicBoothService).
        var booths = await appDbContext.Booths.AsNoTracking()
            .Where(b => b.IsActive && (b.Exhibitor == null || b.Exhibitor.IsActive))
            .OrderBy(b => b.Code)
            .Select(b => new
            {
                b.Id,
                Name = b.Exhibitor != null ? b.Exhibitor.Name : b.ExhibitorName,
                NameArabic = b.Exhibitor != null ? b.Exhibitor.NameArabic : b.ExhibitorNameArabic,
                b.Sector,
                b.SectorArabic,
                LogoContactId = b.Exhibitor != null ? b.Exhibitor.ContactId : null,
                // Country resolved through the Exhibitor -> Contact -> Country
                // navigation (one LEFT JOIN), mirroring the speaker block above.
                CountryId = b.Exhibitor != null && b.Exhibitor.Contact != null
                    ? b.Exhibitor.Contact.CountryId : null,
                CountryName = b.Exhibitor != null && b.Exhibitor.Contact != null
                    && b.Exhibitor.Contact.Country != null
                    ? b.Exhibitor.Contact.Country.Name : null,
                CountryNameArabic = b.Exhibitor != null && b.Exhibitor.Contact != null
                    && b.Exhibitor.Contact.Country != null
                    ? b.Exhibitor.Contact.Country.NameArabic : null,
            })
            .ToListAsync(cancellationToken);
        entries.AddRange(booths.Select(b => new PartnerDirectoryEntry(
            PartnerDirectoryKind.Booth, b.Id, b.Name ?? string.Empty, b.NameArabic ?? string.Empty,
            b.Sector, b.SectorArabic, null, b.LogoContactId,
            b.CountryId, b.CountryName, b.CountryNameArabic)));

        // 4) Opted-in "Other"-type accounts. Pool = Approved, non-Admin users
        //    (Identity DB). Include only non-visitor profile types that opted in;
        //    this is what keeps Normal / VIP out. De-dup: a person already curated
        //    as a Speaker (linked UserProfileId) is dropped — the curated entity wins.
        var approvedIds = await identityDbContext.Users.AsNoTracking()
            .Where(u => u.AccountState == AccountState.Approved
                && u.UserType != UserType.Admin)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (approvedIds.Count > 0)
        {
            // De-dup key from the already-materialized speaker list (no extra query):
            // an opted-in account that is also a curated Speaker is dropped here.
            var curated = speakers
                .Where(s => s.UserProfileId != null)
                .Select(s => s.UserProfileId!.Value)
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
