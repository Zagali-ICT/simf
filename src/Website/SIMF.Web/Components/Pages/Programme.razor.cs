using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;
using SIMF.Web.Content;
using static SIMF.Web.Content.LocalizedText;

namespace SIMF.Web.Components.Pages;

// Website - public programme / agenda page (D-199, Mockup page 16 "Agenda").
// Static-SSR public read over the anonymous backend; groups the published
// sessions by their event-local (+03:00 Riyadh) calendar date into day sections
// and shows an optional best-effort speakers strip. Bilingual selection and the
// time formatting come from the shared LocalizedText / EventTime helpers so this
// page and Session Detail stay consistent. Markup lives in Programme.razor.
public partial class Programme
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    private bool _error;
    private readonly List<DaySection> _days = new();
    private IReadOnlyList<PublicSpeakerSummary> _speakers = Array.Empty<PublicSpeakerSummary>();

    protected override async Task OnInitializedAsync()
    {
        var sessions = await Api.GetProgrammeSessionsAsync();
        if (sessions is null)
        {
            _error = true;
            return;
        }

        BuildDays(sessions.Items ?? Array.Empty<PublicSessionListItem>());

        // The speakers strip is best-effort: a failure to load it must not
        // turn the whole agenda into an error state, so a null result just
        // leaves the strip empty.
        var speakers = await Api.GetSpeakersAsync();
        if (speakers is not null)
        {
            _speakers = speakers.Items;
        }
    }

    // Group sessions by their event-local (+03:00) calendar date, ordered by day
    // and then by start time within each day.
    private void BuildDays(IReadOnlyList<PublicSessionListItem> items)
    {
        var groups = items
            .OrderBy(s => s.StartUtc)
            .GroupBy(s => EventTime.Local(s.StartUtc).Date)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            _days.Add(new DaySection(
                group.Key.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentUICulture),
                group.ToList()));
        }
    }

    // "HH:mm – HH:mm" event-local window for a session row.
    private static string TimeWindow(PublicSessionListItem session) =>
        EventTime.Window(session.StartUtc, session.EndUtc);

    private static string Title(PublicSessionListItem session) =>
        Pick(session.Title, session.TitleArabic);

    private static string Hall(PublicSessionListItem session) =>
        Pick(session.HallName, session.HallNameArabic);

    // Optional: null when the session carries no theme name (in either language),
    // so the razor omits the pill rather than painting an empty chip.
    private static string? ThemeName(PublicSessionListItem session) =>
        PickOrNull(session.PrimaryThemeName, session.PrimaryThemeNameArabic);

    private static string SpeakerName(PublicSpeakerSummary speaker) =>
        Pick(speaker.Name, speaker.NameArabic);

    private sealed record DaySection(string Heading, IReadOnlyList<PublicSessionListItem> Sessions);
}
