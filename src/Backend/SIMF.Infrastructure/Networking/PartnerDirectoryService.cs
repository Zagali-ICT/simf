// Tests: SIMF.Api.Tests/PartnerDirectoryServiceTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Networking.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Networking;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Networking;

/// <summary>
/// The "Meet People Like You" partner directory. A deduped union of the
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
/// <para>Two-DB: the account pool comes from the Identity DB in its own
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
            s.Rank, s.RankArabic,
            // This field is a PRESENCE SENTINEL, not a path the client loads:
            // partner_directory_models.dart only tests it for null and then
            // builds /app/assets/SpeakerPhoto/{id}/image itself. It used to
            // carry Speaker.PhotoRelativePath, a column that no longer exists,
            // so it now carries that same served path when the speaker actually
            // has a photo in the store. Emitting null here instead would drop
            // every partner-directory speaker to an initials avatar on devices
            // already in the field.
            s.HasPhotoAsset ? SpeakerPhotoAssetPath(s.Id) : null, null,
            s.CountryId, s.CountryNameEn, s.CountryNameAr)));

        // 2) Sponsors — the same public list (Contact-first name / logo / country,
        //    tagline as the subtitle), flattened out of the tier groups. Logo served
        //    by sponsor id (SponsorLogo). Tap → sponsor detail.
        var sponsors = await sponsorService.ListAsync(cancellationToken);
        entries.AddRange(sponsors.Groups
            .SelectMany(g => g.Sponsors)
            .Select(s => new PartnerDirectoryEntry(
                PartnerDirectoryKind.Sponsor, s.Id, s.NameEn, s.NameAr,
                s.Tagline, s.TaglineArabic,
                // The same presence sentinel as the speaker branch above, and
                // the same trap: this used to carry Sponsor.LogoRelativePath,
                // which is now permanently null, so reading it would have shown
                // every sponsor in the directory as an initials tile with no
                // compiler error and no failing test to say so.
                s.HasLogo ? SponsorLogoAssetPath(s.Id) : null, null,
                s.CountryId, s.CountryNameEn, s.CountryNameAr)));

        // Sponsor / booth-company de-dup key (owner decision 2026-07-22): a company
        // that is BOTH a Sponsor and a booth exhibitor must appear ONCE — as the
        // Sponsor (kind=sponsor, routing to the sponsor detail page). The sole key
        // is a case-insensitive, trimmed display-name match over the sponsor names
        // (the former shared-Contact key is gone with the Contact directory).
        var sponsorNameKeys = sponsors.Groups
            .SelectMany(g => g.Sponsors)
            .SelectMany(s => new[] { s.NameEn, s.NameAr })
            .Select(NormalizeNameKey)
            .Where(key => key.Length > 0)
            .ToHashSet();

        // 3) Booth companies — the same public booth list, keyed by booth id (the
        //    exhibitor-detail route takes a boothId). Company name from the linked
        //    Exhibitor, sector as subtitle. The entry carries Id = boothId and the app
        //    renders the booth logo (BoothLogo) from that, so LogoContactId now emits
        //    null on the wire (append-only frozen field). Country via the booth
        //    service. Tap → exhibitor detail. A company already present as a Sponsor
        //    is dropped here (the Sponsor wins).
        var booths = await boothService.ListAsync(cancellationToken);
        foreach (var b in booths)
        {
            var duplicatesSponsor =
                sponsorNameKeys.Contains(NormalizeNameKey(b.ExhibitorName))
                || sponsorNameKeys.Contains(NormalizeNameKey(b.ExhibitorNameArabic));
            if (duplicatesSponsor) { continue; }

            entries.Add(new PartnerDirectoryEntry(
                PartnerDirectoryKind.Booth, b.Id, b.ExhibitorName ?? string.Empty,
                b.ExhibitorNameArabic ?? string.Empty, b.Sector, b.SectorArabic,
                null, null, b.CountryId, b.CountryName, b.CountryNameArabic));
        }

        // 4) Opted-in "Other"-type accounts. Pool = Approved, non-Admin users
        //    (Identity DB, shared with the recommender via WhereApprovedNonAdmin).
        //    Include only non-visitor profile types that opted in; this is what
        //    keeps Normal / VIP out. De-dup: a person already curated as a Speaker
        //    (linked UserProfileId) is dropped — the curated entity wins. An
        //    attendee holding no account is not in that pool by construction, so
        //    the account-less profiles are skipped rather than matched.
        //
        //    Asked App-side FIRST, then Identity. The partner types are a handful
        //    of profiles out of the whole attendee body, so enumerating every
        //    approved account and shipping the lot across as an IN (...) list
        //    moved twenty thousand ids to select eighty rows. Selecting the
        //    candidates first and asking Identity only about THEIR account ids is
        //    the same set by the same two predicates, two orders of magnitude
        //    smaller.
        var candidates = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId != null
                && p.ShowInMeetLikeYou
                && p.ProfileType != null && !p.ProfileType.IsForVisitor
                // Admin master switch — hiding a partner type drops
                // ALL its accounts here (AND with the per-user opt-in).
                && p.ProfileType.ShowInPartnerDirectory)
            .OrderBy(p => p.NameArabic).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                UserId = p.UserId!.Value,
                p.Name,
                p.NameArabic,
                p.JobTitle,
                p.JobTitleArabic,
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count > 0)
        {
            var candidateAccountIds = candidates
                .Select(p => p.UserId)
                .Distinct()
                .ToList();
            var approvedIds = (await identityDbContext.Users.AsNoTracking()
                .WhereApprovedNonAdmin()
                .Where(u => candidateAccountIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            // The user-profile ids already curated as active Speakers. Read as a
            // minimal projection here (not from the public speaker DTO, which
            // deliberately never surfaces the UserProfileId account link) purely
            // for the de-dup below.
            var curated = (await appDbContext.Speakers.AsNoTracking()
                .Where(s => s.IsActive && s.UserProfileId != null)
                .Select(s => s.UserProfileId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            // Filtered in the SQL order the candidates came back in, so the
            // directory keeps its Arabic-name-then-English-name ordering.
            entries.AddRange(candidates
                .Where(p => approvedIds.Contains(p.UserId) && !curated.Contains(p.Id))
                .Select(p => new PartnerDirectoryEntry(
                    PartnerDirectoryKind.Person, p.Id, p.Name, p.NameArabic,
                    p.JobTitle, p.JobTitleArabic, null, null, null, null, null)));
        }

        return new PartnerDirectoryResponse(entries);
    }

    /// <summary>Case-insensitive, trimmed name key — the sole sponsor / booth-company
    /// de-dup key now that the shared Contact directory is gone.</summary>
    private static string NormalizeNameKey(string? name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>The served path for a speaker's photo, which is what the app
    /// builds for itself from the id. Relative, never absolute — the client
    /// prepends its own base URL.</summary>
    private static string SpeakerPhotoAssetPath(Guid speakerId) =>
        $"/app/assets/{nameof(AssetCategory.SpeakerPhoto)}/{speakerId}/image";

    private static string SponsorLogoAssetPath(Guid sponsorId) =>
        $"/app/assets/{nameof(AssetCategory.SponsorLogo)}/{sponsorId}/image";
}
