using Microsoft.Extensions.Caching.Memory;
using SIMF.ApiClient;
using SIMF.Common;

namespace SIMF.Web.Content;

/// <summary>
/// Resolves the CP-editable forum event dates
/// (<c>OrganizationProfile.EventStartDate</c> / <c>EventEndDate</c>) once per short
/// cache window and formats them as the shared bilingual <see cref="EventDateRange"/>,
/// so every public page renders the same config-driven date instead of a hardcoded
/// literal. A null / unreachable profile (or a profile with no dates) yields
/// <c>null</c> and the caller falls back to its resx date label — the marketing page
/// never blanks, and a transient API outage is not cached.
/// </summary>
public sealed class ForumDates(SimfPublicClient api, IMemoryCache cache)
{
    private const string CacheKey = "simf.web.forum-event-dates";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    /// <summary>The event date range for the active culture, or <c>null</c> when the
    /// profile is unavailable or carries no dates (the caller then renders its resx
    /// fallback label).</summary>
    public async Task<string?> GetRangeDisplayAsync(
        bool arabic, CancellationToken cancellationToken = default)
    {
        if (await GetDatesAsync(cancellationToken) is not { } range)
        {
            return null;
        }
        return EventDateRange.Format(range.Start, range.End, arabic);
    }

    private async Task<(DateOnly Start, DateOnly End)?> GetDatesAsync(
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out (DateOnly Start, DateOnly End) cached))
        {
            return cached;
        }

        var profile = await api.GetOrganizationProfileAsync(cancellationToken);
        if (profile?.EventStartDate is not { } start || profile.EventEndDate is not { } end)
        {
            // A miss is deliberately NOT cached, so a transient outage retries.
            return null;
        }

        var range = (Start: DateOnly.FromDateTime(start.Date), End: DateOnly.FromDateTime(end.Date));
        cache.Set(CacheKey, range, CacheFor);
        return range;
    }
}
