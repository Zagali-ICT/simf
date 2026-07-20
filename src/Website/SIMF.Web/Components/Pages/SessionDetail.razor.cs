using System.Globalization;
using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;
using SIMF.Web.Content;
using static SIMF.Web.Content.LocalizedText;

namespace SIMF.Web.Components.Pages;

// Code-behind for the public Session Detail page (/sessions/{id} — Figma
// 5991-85840). Built on the shared LandingShell chrome; the session comes from
// the anonymous public API (SimfPublicClient.GetSessionAsync) fetched during
// static SSR, plus a best-effort "related sessions" strip from the agenda list.
// A null/unknown id renders the not-found state; the page never throws.
public partial class SessionDetail
{
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private PublicSessionDetail? Session { get; set; }
    private IReadOnlyList<PublicSessionListItem> Related { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        Session = await Api.GetSessionAsync(Id);
        if (Session is null)
        {
            return;
        }

        // Best-effort related strip: up to three other sessions from the agenda
        // (this one excluded), earliest first. A null agenda result just leaves it
        // empty — it must not break the page.
        var agenda = await Api.GetProgrammeSessionsAsync();
        Related = (agenda?.Items ?? [])
            .Where(item => item.Id != Id)
            .OrderBy(item => item.StartUtc)
            .Take(3)
            .ToList();
    }

    // Optional page-level values used by more than one section (declared here,
    // not leaked across razor sections): the description lead + the category.
    private string? Lead(PublicSessionDetail s) => PickOrNull(s.Description, s.DescriptionArabic);
    private string? Category(PublicSessionDetail s) => PickOrNull(s.CategoryName, s.CategoryNameArabic);

    private static string TimeRange(PublicSessionDetail s) =>
        EventTime.Window(s.StartUtc, s.EndUtc);

    private static string DateLabel(PublicSessionDetail s) =>
        EventTime.Local(s.StartUtc).ToString("dddd d MMMM yyyy", CultureInfo.CurrentUICulture);

    private static string WeekdayLabel(PublicSessionDetail s) =>
        EventTime.Local(s.StartUtc).ToString("dddd", CultureInfo.CurrentUICulture);

    // Related-strip card helpers (agenda list items).
    private static string RelatedTitle(PublicSessionListItem s) => Pick(s.Title, s.TitleArabic);
    private static string RelatedHall(PublicSessionListItem s) => Pick(s.HallName, s.HallNameArabic);
    private static string RelatedTime(PublicSessionListItem s) =>
        EventTime.Window(s.StartUtc, s.EndUtc);

    // Speaker-card helpers (reuse the ln-spkcard family from the Speakers page).
    private static string SpeakerName(PublicSessionSpeaker s) => Pick(s.Name, s.NameArabic);
    private static string? SpeakerCountry(PublicSessionSpeaker s) =>
        PickOrNull(s.CountryNameEn, s.CountryNameAr);

    private static string SpeakerPhotoUrl(PublicSessionSpeaker s) =>
        SpeakerPhoto.Url(s.Id, s.HasPhotoAsset, s.PhotoRelativePath);

    // Theme-card helpers.
    private static string ThemeName(PublicSessionTheme t) => Pick(t.Name, t.NameArabic);
    private static string? ThemeDescription(PublicSessionTheme t) =>
        PickOrNull(t.Description, t.DescriptionArabic);

    // Outcome text.
    private static string OutcomeText(PublicSessionOutcome o) => Pick(o.Text, o.TextArabic);

    // Same-origin public download route the page links each file to.
    private string DownloadUrl(PublicSessionDownload d) =>
        $"/content/sessions/{Id}/downloads/{d.Id}";

    private static string FileSize(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:0.#} MB"
        : bytes >= 1_024 ? $"{bytes / 1_024.0:0.#} KB"
        : $"{bytes} B";
}
