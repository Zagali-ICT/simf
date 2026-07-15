using System.Globalization;
using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;

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

    // The forum runs on Riyadh time (+03:00); the public projection buckets days
    // the same way, so the hero/at-a-glance render the session in event-local time.
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    private static bool Rtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    protected override async Task OnInitializedAsync()
    {
        Session = await Api.GetSessionAsync(Id);
        if (Session is null)
        {
            return;
        }

        // Best-effort related strip: other upcoming sessions (never this one). A
        // null agenda result just leaves it empty — it must not break the page.
        var agenda = await Api.GetProgrammeSessionsAsync();
        Related = (agenda?.Items ?? [])
            .Where(item => item.Id != Id)
            .OrderBy(item => item.StartUtc)
            .Take(3)
            .ToList();
    }

    // Arabic-preferred in RTL, English-preferred in LTR; falls back to the other.
    private static string Pick(string en, string ar) =>
        Rtl ? (string.IsNullOrWhiteSpace(ar) ? en : ar)
            : (string.IsNullOrWhiteSpace(en) ? ar : en);

    private static string? PickOrNull(string? en, string? ar)
    {
        var value = Rtl
            ? (string.IsNullOrWhiteSpace(ar) ? en : ar)
            : (string.IsNullOrWhiteSpace(en) ? ar : en);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string TimeRange(PublicSessionDetail s) =>
        $"{s.StartUtc.ToOffset(EventOffset):HH:mm} – {s.EndUtc.ToOffset(EventOffset):HH:mm}";

    private static string DateLabel(PublicSessionDetail s) =>
        s.StartUtc.ToOffset(EventOffset)
            .ToString("dddd d MMMM yyyy", CultureInfo.CurrentUICulture);

    private static string WeekdayLabel(PublicSessionDetail s) =>
        s.StartUtc.ToOffset(EventOffset)
            .ToString("dddd", CultureInfo.CurrentUICulture);

    // Related-strip card helpers (agenda list items).
    private static string RelatedTitle(PublicSessionListItem s) => Pick(s.Title, s.TitleArabic);
    private static string RelatedHall(PublicSessionListItem s) => Pick(s.HallName, s.HallNameArabic);
    private static string RelatedTime(PublicSessionListItem s) =>
        $"{s.StartUtc.ToOffset(EventOffset):HH:mm} – {s.EndUtc.ToOffset(EventOffset):HH:mm}";

    // Speaker-card helpers (reuse the ln-spkcard family from the Speakers page).
    private static string SpeakerName(PublicSessionSpeaker s) => Pick(s.Name, s.NameArabic);
    private static string? SpeakerCountry(PublicSessionSpeaker s) =>
        PickOrNull(s.CountryNameEn, s.CountryNameAr);

    // Same-origin photo route (StoredFile SpeakerPhoto) → legacy path → none,
    // matching the Speakers page so a real portrait renders when one exists.
    private static string SpeakerPhotoUrl(PublicSessionSpeaker s) =>
        s.HasPhotoAsset ? $"/content/assets/SpeakerPhoto/{s.Id}/image"
        : !string.IsNullOrWhiteSpace(s.PhotoRelativePath) ? s.PhotoRelativePath!
        : string.Empty;

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
