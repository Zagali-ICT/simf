using System.Globalization;
using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public Speakers page (/speakers — Figma 5840-26779).
// Renders the shared LandingShell chrome + an event page-title band + a live
// speaker grid. Speaker data comes from the anonymous public API
// (SimfPublicClient) fetched server-side during static SSR, exactly like
// Programme.razor — a null/unreachable result just leaves the grid empty.
public partial class Speakers
{
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    private IReadOnlyList<PublicSpeakerSummary> SpeakerList { get; set; } = [];

    private static bool Rtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    protected override async Task OnInitializedAsync()
    {
        var result = await Api.GetSpeakersAsync();
        SpeakerList = result?.Items ?? [];
    }

    // Arabic-preferred in RTL, English-preferred in LTR; fall back to the other.
    private static string DisplayName(PublicSpeakerSummary s) =>
        Rtl
            ? (string.IsNullOrWhiteSpace(s.NameArabic) ? s.Name : s.NameArabic)
            : (string.IsNullOrWhiteSpace(s.Name) ? s.NameArabic : s.Name);

    // Location = country. The public API exposes country only (no city); a
    // city-level field would need an additive backend change — tracked follow-up.
    private static string? LocationName(PublicSpeakerSummary s) =>
        Rtl ? s.CountryNameAr : s.CountryNameEn;

    // Same-origin photo route (StoredFile SpeakerPhoto) → legacy path → none.
    // Mirrors SiteContentEndpoints.MapSpeakers so a real portrait renders when
    // the speaker has one; empty string → the card shows its gradient backdrop.
    private static string PhotoUrl(PublicSpeakerSummary s) =>
        s.HasPhotoAsset ? $"/content/assets/SpeakerPhoto/{s.Id}/image"
        : !string.IsNullOrWhiteSpace(s.PhotoRelativePath) ? s.PhotoRelativePath!
        : string.Empty;
}
