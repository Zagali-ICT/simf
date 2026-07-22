using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using SIMF.ApiClient;
using SIMF.Contracts.Archive;
using SIMF.Web.Components.Pages;

namespace SIMF.Web.Content;

// One ordered past-edition (newest-first), shared by BOTH the /archive page cards
// and the top-nav Archive dropdown so the two can never diverge. AnchorId is the
// stable in-page id the card renders and the dropdown links to (/archive#ed-N);
// NavLabel is the dropdown text ("title year").
public sealed record PublicEdition(
    string AnchorId,
    Bilingual NavLabel,
    string Image,
    Bilingual Date,
    Bilingual Name,
    Bilingual Text)
{
    // The dropdown target: the edition's card on the single /archive page (there is
    // no per-year public page/endpoint — GetArchiveAsync returns the whole list).
    public string Href => $"/archive#{AnchorId}";
}

// The editions plus the latest edition's headline stats, resolved together as one
// cached view (the /archive page needs both; the nav needs only the editions).
public sealed record EditionsView(
    IReadOnlyList<PublicEdition> Editions,
    string Speakers,
    string Attendees,
    string Sessions);

/// <summary>
/// Resolves the public past editions once per short cache window. The nav renders
/// on every page, so an uncached <see cref="SimfPublicClient.GetArchiveAsync"/>
/// would cost one API read per page view. Live editions when the archive-visibility
/// toggle (D-166) is on; otherwise the landing's static Milestones — the same
/// graceful fallback the /archive page already used, so the nav dropdown and the
/// page always show the same list.
/// </summary>
public sealed class PublicEditions(SimfPublicClient api, IMemoryCache cache)
{
    private const string CacheKey = "simf.web.public-editions";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    // Default headline stats when no live edition carries them (mirrors the value
    // the /archive page used before this became the single source).
    private const string DefaultSpeakers = "+250";
    private const string DefaultAttendees = "+375";
    private const string DefaultSessions = "+30";

    // Fallback cover photos (reused milestone images) cycled by index when a live
    // edition carries no publicly-servable cover.
    private static readonly string[] FallbackCovers =
    [
        "assets/figma/milestones/card3-innovation.jpg",
        "assets/figma/milestones/card2-digital.jpg",
        "assets/figma/milestones/card4-secure-lanes.jpg",
    ];

    public async Task<EditionsView> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out EditionsView? cached) && cached is not null)
        {
            return cached;
        }

        PublicArchive? result;
        try
        {
            result = await api.GetArchiveAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The client already returns null (not throws) on a transient failure;
            // this only guards a malformed payload. Either way it is a failure.
            result = null;
        }

        // A transient failure surfaces as null (network error, timeout, a non-JSON
        // error page) — serve the static fallback but do NOT cache it, so the next
        // request retries the live source. A genuinely hidden archive returns a
        // non-null empty list, which IS a real answer and is cached below.
        if (result is null)
        {
            return Build([]);
        }

        var view = Build(result.Items);
        cache.Set(CacheKey, view, CacheFor);
        return view;
    }

    // Live editions (newest-first) when present; else the landing's past Milestones
    // (drop the "future" card, newest-first). Index-based AnchorId keeps the card id
    // and the dropdown href aligned in either branch. Pure + internal so the mapping
    // is unit-testable without the API.
    internal static EditionsView Build(IReadOnlyList<PublicArchiveEdition> items)
    {
        if (items.Count > 0)
        {
            var ordered = items.OrderByDescending(e => e.Year).ToList();
            var editions = ordered.Select((e, i) =>
            {
                // TitleAr/En are non-nullable on the contract, but System.Text.Json
                // does not enforce it — coalesce so a null title never NREs here.
                var title = new Bilingual(e.TitleAr ?? string.Empty, e.TitleEn ?? string.Empty);
                return new PublicEdition(
                    AnchorId: $"ed-{i}",
                    NavLabel: Label(title, e.Year),
                    Image: !string.IsNullOrWhiteSpace(e.CoverImageRelativePath)
                        ? e.CoverImageRelativePath!
                        : FallbackCovers[i % FallbackCovers.Length],
                    Date: new Bilingual(e.DateLabelAr ?? e.Year.ToString(), e.DateLabelEn ?? e.Year.ToString()),
                    Name: title,
                    Text: new Bilingual(e.SummaryAr ?? string.Empty, e.SummaryEn ?? string.Empty));
            }).ToList();
            var latest = ordered[0];
            return new EditionsView(editions, $"+{latest.Speakers}", $"+{latest.Attendees}", $"+{latest.Sessions}");
        }

        var fallback = Landing.Milestones
            .Where(m => !m.IsFuture)
            .Reverse()
            .Select((m, i) => new PublicEdition(
                AnchorId: $"ed-{i}",
                NavLabel: Label(m.Name, ParseYear(m.Date)),
                Image: m.Image,
                Date: m.Date,
                Name: m.Name,
                Text: m.Text))
            .ToList();
        return new EditionsView(fallback, DefaultSpeakers, DefaultAttendees, DefaultSessions);
    }

    // "title year" (space-joined), unless the title already carries the year.
    internal static Bilingual Label(Bilingual title, int year)
    {
        if (year <= 0)
        {
            return title;
        }
        var y = year.ToString();
        return new Bilingual(
            title.Ar.Contains(y) ? title.Ar : $"{title.Ar} {y}",
            title.En.Contains(y) ? title.En : $"{title.En} {y}");
    }

    // First 4-digit run in the date label — the Milestones fallback has no Year
    // field, but every date string carries the year (e.g. "24–26 November 2019").
    private static int ParseYear(Bilingual date)
    {
        var match = Regex.Match(date.En, "\\d{4}");
        return match.Success && int.TryParse(match.Value, out var year) ? year : 0;
    }
}
