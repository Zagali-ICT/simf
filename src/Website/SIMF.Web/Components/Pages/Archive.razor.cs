using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Contracts.Archive;
using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public Archive page (/archive — Figma 5840-27997). Renders
// the shared LandingShell chrome + a live-data past-editions archive: the headline
// counters + the edition cards come from the anonymous public API
// (SimfPublicClient.GetArchiveAsync), fetched server-side during static SSR. When
// the archive-visibility toggle is off (Items empty) or the service is unreachable,
// the page falls back to the landing's static Milestones (past editions) so it never
// blanks — the same graceful-degradation contract as the other ln- pages. The
// photo gallery + session titles + past speakers reuse shared content/components:
// the public API exposes no per-edition detail (gallery / session titles / past
// speakers) yet, so those are static/reused pending a public endpoint (web/archive.md §7).
public partial class Archive
{
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    // A unified edition card the ln-miles view walks — populated from the live
    // archive editions when available, else the landing's static past editions.
    public sealed record EditionCard(string Image, Bilingual Date, Bilingual Name, Bilingual Text);

    // The three headline counters on the navy stats band.
    public sealed record StatTriple(string Speakers, string Attendees, string Sessions);

    // Static highlights gallery (real forum photos). The live archive-media feed +
    // a video provider are a follow-up (web/archive.md §7).
    public static readonly IReadOnlyList<string> GalleryPhotos =
    [
        "assets/figma/news/news-1.jpg",
        "assets/figma/sessions/session-card-1.jpg",
        "assets/figma/news/news-2.jpg",
        "assets/figma/sessions/session-card-2.jpg",
        "assets/figma/news/news-3.jpg",
        "assets/figma/sessions/session-card-3.jpg",
    ];

    // Fallback cover photos (reused milestone images) cycled by index when a live
    // edition carries no publicly-servable cover.
    private static readonly string[] FallbackCovers =
    [
        "assets/figma/milestones/card3-innovation.jpg",
        "assets/figma/milestones/card2-digital.jpg",
        "assets/figma/milestones/card4-secure-lanes.jpg",
    ];

    private IReadOnlyList<EditionCard> Editions { get; set; } = [];
    private StatTriple Stats { get; set; } = new("+250", "+375", "+30");

    protected override async Task OnInitializedAsync()
    {
        var result = await Api.GetArchiveAsync();
        var items = result?.Items ?? [];
        if (items.Count > 0)
        {
            var ordered = items.OrderByDescending(e => e.Year).ToList();
            Editions = [.. ordered.Select(MapEdition)];
            var latest = ordered[0];
            Stats = new($"+{latest.Speakers}", $"+{latest.Attendees}", $"+{latest.Sessions}");
        }
        else
        {
            // static fallback — the landing's past editions, newest-first (Reverse:
            // Milestones are declared oldest-first) to match the live ordering; drop
            // the "future" card. The default StatTriple keeps the headline populated.
            Editions = [.. Landing.Milestones
                .Where(m => !m.IsFuture)
                .Reverse()
                .Select(m => new EditionCard(m.Image, m.Date, m.Name, m.Text))];
        }
    }

    private static EditionCard MapEdition(PublicArchiveEdition e, int index)
    {
        var image = !string.IsNullOrWhiteSpace(e.CoverImageRelativePath)
            ? e.CoverImageRelativePath!
            : FallbackCovers[index % FallbackCovers.Length];
        var date = new Bilingual(e.DateLabelAr ?? e.Year.ToString(), e.DateLabelEn ?? e.Year.ToString());
        var name = new Bilingual(e.TitleAr, e.TitleEn);
        var text = new Bilingual(e.SummaryAr ?? string.Empty, e.SummaryEn ?? string.Empty);
        return new EditionCard(image, date, name, text);
    }
}
