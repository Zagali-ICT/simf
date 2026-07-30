// Tests: SIMF.Api.Tests/DelegationsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Contracts.Delegations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Delegations;

/// <summary>D-499 (Figma 1426:10771 الوفود) — builds the public delegations view:
/// one card per invited active country (in the picker order), each with its head of
/// delegation (resolved from the country's pointer), date range and member count,
/// plus the two aggregate stats. All reads stay on <c>SimfAppDbContext</c> — Country
/// + UserProfile both live there, so there is no cross-DB join (D-157).
///
/// <para>G2 (D-800) — the view is per-viewer: a signed-in caller never sees their
/// OWN delegation, i.e. the country whose id equals the caller's
/// <c>UserProfile.NationalityId</c> is dropped. An anonymous caller passes a null
/// viewer id and gets the full list.</para></summary>
internal sealed class PublicDelegationService(SimfAppDbContext appDbContext)
    : IPublicDelegationService
{
    public async Task<AppDelegations> GetAsync(
        Guid? viewerUserId, CancellationToken cancellationToken = default)
    {
        var viewerCountryId = await ResolveViewerCountryIdAsync(viewerUserId, cancellationToken);

        var invitedCountries = appDbContext.Countries.AsNoTracking()
            .Where(country => country.IsInvited && country.IsActive);

        // G2 (D-800) — drop the viewer's own country BEFORE anything else reads the
        // set, so the member-count group-by and both aggregate stats below are
        // computed over exactly the countries the viewer is shown.
        if (viewerCountryId is { } excludedCountryId)
        {
            invitedCountries = invitedCountries.Where(country => country.Id != excludedCountryId);
        }

        var countries = await invitedCountries
            .OrderBy(country => country.DisplayOrder)
            .ThenBy(country => country.NameArabic)
            .Select(country => new
            {
                country.Id,
                country.Code,
                country.Name,
                country.NameArabic,
                country.DelegationArrivalDate,
                country.DelegationDepartureDate,
                country.HeadOfDelegationUserProfileId,
            })
            .ToListAsync(cancellationToken);

        if (countries.Count == 0)
        {
            return new AppDelegations(0, 0, Array.Empty<AppDelegationItem>());
        }

        var invitedIds = countries.Select(c => c.Id).ToList();

        // Member counts — active delegate profiles whose nationality is an invited
        // country, grouped by nationality. Counts registered delegates regardless of
        // account-approval state (that lives on the Identity DB; a cross-DB join is
        // barred by D-157) — a headcount of active delegate profiles is the
        // single-DB answer.
        var memberCounts = await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.IsActive
                && profile.IsDelegate
                && invitedIds.Contains(profile.NationalityId))
            .GroupBy(profile => profile.NationalityId)
            .Select(group => new { NationalityId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.NationalityId, row => row.Count, cancellationToken);

        // Heads — batch-resolve the designated head profiles in one query.
        var headIds = countries
            .Where(c => c.HeadOfDelegationUserProfileId.HasValue)
            .Select(c => c.HeadOfDelegationUserProfileId!.Value)
            .Distinct()
            .ToList();
        var headsById = headIds.Count == 0
            ? new Dictionary<Guid, HeadCard>()
            : await appDbContext.UserProfiles.AsNoTracking()
                // Re-check IsDelegate at read time (symmetric with the write-time
                // ValidateHeadAsync guard) so a profile that ever stopped being a
                // delegate is never published as a head.
                .Where(profile => headIds.Contains(profile.Id)
                    && profile.IsActive
                    && profile.IsDelegate)
                .Select(profile => new HeadCard(
                    profile.Id, profile.Name, profile.NameArabic,
                    profile.JobTitle ?? profile.Honorific,
                    profile.JobTitleArabic ?? profile.HonorificArabic))
                .ToDictionaryAsync(card => card.Id, cancellationToken);

        var items = countries
            .Select(c =>
            {
                var head = c.HeadOfDelegationUserProfileId is { } hid
                    ? headsById.GetValueOrDefault(hid)
                    : null;
                return new AppDelegationItem(
                    c.Id,
                    c.Code,
                    c.Name,
                    c.NameArabic,
                    head?.Name,
                    head?.NameArabic,
                    head?.Title,
                    c.DelegationArrivalDate,
                    c.DelegationDepartureDate,
                    memberCounts.GetValueOrDefault(c.Id, 0),
                    head?.ArabicTitle);
            })
            .ToList();

        return new AppDelegations(items.Count, items.Sum(i => i.MemberCount), items);
    }

    /// <summary>G2 (D-800) — the country id to hide from this viewer: their profile's
    /// nationality. Null for an anonymous caller and for a signed-in caller with no
    /// profile row (an Admin / CP user), who both see the full list. Same-database
    /// read — UserProfile is on <c>SimfAppDbContext</c> alongside Country, so D-157
    /// is not engaged.
    ///
    /// <para>The <c>IsActive</c> filter matches the member-count query above and the
    /// sibling profile lookup in <c>DelegationMeetingRequestService</c>: a
    /// soft-deleted profile is counted nowhere else, so it must not silently hide a
    /// delegation either.</para></summary>
    private async Task<int?> ResolveViewerCountryIdAsync(
        Guid? viewerUserId, CancellationToken cancellationToken)
    {
        if (viewerUserId is not { } userId)
        {
            return null;
        }

        return await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.IsActive)
            .Select(profile => (int?)profile.NationalityId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The head-of-delegation fields resolved from the pointed-at profile.</summary>
    private sealed record HeadCard(
        Guid Id, string Name, string NameArabic, string? Title, string? ArabicTitle);
}
