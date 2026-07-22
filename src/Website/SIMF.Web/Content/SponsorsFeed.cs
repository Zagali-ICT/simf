using SIMF.ApiClient;

namespace SIMF.Web.Content;

/// <summary>One sponsor card for the shared sponsors marquee (the landing band +
/// the dedicated <c>/partners</c> page): the sponsor's display name (rendered as
/// a wordmark) plus a bilingual tier pill.</summary>
public sealed record SponsorCard(Bilingual Name, Bilingual Tier);

/// <summary>
/// Loads the public sponsor roster (<c>GET /api/v1/app/sponsors</c>) and flattens
/// the tier groups — already highest-tier-first from the API — into
/// <see cref="SponsorCard"/>s for the marquee. A null / unreachable envelope
/// yields an empty list, so the band hides rather than shipping a placeholder.
/// A thin Content-layer wrapper over <see cref="SimfPublicClient"/>, mirroring
/// <see cref="ForumDates"/>; shared so the landing and /partners never drift.
/// </summary>
public static class SponsorsFeed
{
    public static async Task<IReadOnlyList<SponsorCard>> LoadAsync(
        SimfPublicClient api, CancellationToken cancellationToken = default)
    {
        var sponsors = await api.GetSponsorsAsync(cancellationToken);
        if (sponsors is null)
        {
            return [];
        }
        return
        [
            .. sponsors.Groups
                .SelectMany(group => group.Sponsors)
                .Select(sponsor => new SponsorCard(
                    new Bilingual(sponsor.NameAr, sponsor.NameEn),
                    TierLabel(sponsor.TierName)))
        ];
    }

    // Maps the API tier name (the English SponsorTier enum name) to a bilingual
    // pill label. An unknown tier falls back to the raw name in both languages.
    private static Bilingual TierLabel(string tierName) => tierName switch
    {
        "Platinum" => new("راعٍ بلاتيني", "Platinum sponsor"),
        "Gold" => new("راعٍ ذهبي", "Gold sponsor"),
        "Silver" => new("راعٍ فضي", "Silver sponsor"),
        "Bronze" => new("راعٍ برونزي", "Bronze sponsor"),
        _ => new(tierName, tierName),
    };
}
