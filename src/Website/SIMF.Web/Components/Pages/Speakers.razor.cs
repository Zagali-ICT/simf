using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;
using SIMF.Web.Content;
using static SIMF.Web.Content.LocalizedText;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public Speakers page (/speakers — Figma 5840-26779).
// Renders the shared LandingShell chrome + an event page-title band + a live
// speaker grid. Speaker data comes from the anonymous public API
// (SimfPublicClient) fetched server-side during static SSR, exactly like
// Programme.razor — a null/unreachable result just leaves the grid empty.
public partial class Speakers
{
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    [Inject] private ForumDates Dates { get; set; } = default!;

    private IReadOnlyList<PublicSpeakerSummary> SpeakerList { get; set; } = [];

    // The CP-editable forum date range for the page-title band; null falls back to
    // the Speakers.Band.Date resx label.
    private string? BandDate { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var result = await Api.GetSpeakersAsync();
        SpeakerList = result?.Items ?? [];
        BandDate = await Dates.GetRangeDisplayAsync(Rtl);
    }

    // Arabic-preferred in RTL, English-preferred in LTR; fall back to the other.
    private static string DisplayName(PublicSpeakerSummary s) => Pick(s.Name, s.NameArabic);

    // Location = country. The public API exposes country only (no city); a
    // city-level field would need an additive backend change — tracked follow-up.
    private static string? LocationName(PublicSpeakerSummary s) =>
        Rtl ? s.CountryNameAr : s.CountryNameEn;

    // Same-origin photo route (StoredFile SpeakerPhoto) → legacy path → none.
    private static string PhotoUrl(PublicSpeakerSummary s) =>
        SpeakerPhoto.Url(s.Id, s.HasPhotoAsset);
}
