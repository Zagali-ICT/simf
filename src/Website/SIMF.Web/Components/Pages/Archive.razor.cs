using Microsoft.AspNetCore.Components;
using SIMF.Web.Content;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public Archive page (/archive — Figma 5840-27997). Renders
// the shared LandingShell chrome + a live-data past-editions archive. The edition
// cards + headline counters come from PublicEditions (the single source shared with
// the top-nav Archive dropdown, so page and nav never diverge): live editions from
// the anonymous public API when the archive-visibility toggle is on, else the
// landing's static Milestones — the page never blanks. The photo gallery + session
// titles + past speakers reuse shared content/components: the public API exposes no
// per-edition detail (gallery / session titles / past speakers) yet, so those are
// static/reused pending a public endpoint (web/archive.md §7).
public partial class Archive
{
    [Inject] private PublicEditions EditionsSource { get; set; } = default!;

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

    // The past editions (newest-first), each carrying the anchor id the top-nav
    // dropdown links to (/archive#ed-N). Both are (re)assigned in OnInitializedAsync
    // before the SSR render; the empty stat placeholder never reaches the DOM, so the
    // default figures live in one place only — PublicEditions.
    private IReadOnlyList<PublicEdition> Editions { get; set; } = [];
    private StatTriple Stats { get; set; } = new("", "", "");

    protected override async Task OnInitializedAsync()
    {
        var view = await EditionsSource.GetAsync();
        Editions = view.Editions;
        Stats = new StatTriple(view.Speakers, view.Attendees, view.Sessions);
    }
}
