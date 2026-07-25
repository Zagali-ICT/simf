using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Web.Content;
using static SIMF.Web.Content.LocalizedText;

namespace SIMF.Web.Components.Pages;

// Website - public programme / agenda page (D-199, Mockup page 16 "Agenda").
// Static-SSR public read over the anonymous backend; groups the published
// sessions by their event-local (+03:00 Riyadh) calendar date into day sections
// (a day strip switches between them) and renders each session as a timeline
// card (time column + category chip + title + hall + description). The design
// echoes the app "Programme schedule" (Figma 883:2308) adapted to the ln- kit.
// Bilingual selection + time formatting come from the shared LocalizedText /
// EventTime helpers. Markup lives in Programme.razor.
public partial class Programme
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfPublicClient Api { get; set; } = default!;

    private bool _error;
    private readonly List<DaySection> _days = new();
    private IReadOnlyList<SessionType> _types = Array.Empty<SessionType>();
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
    // and then by start time within each day. Each day gets a stable index id the
    // day strip / JS filter target, plus the localized weekday + date number the
    // pill shows. The distinct session types drive the (optional) filter tabs.
    private void BuildDays(IReadOnlyList<PublicSessionListItem> items)
    {
        var groups = items
            .OrderBy(s => s.Start)
            .GroupBy(s => EventTime.Local(s.Start).Date)
            .OrderBy(g => g.Key)
            .ToList();

        for (var i = 0; i < groups.Count; i++)
        {
            var day = groups[i].Key;
            _days.Add(new DaySection(
                i.ToString(CultureInfo.InvariantCulture),
                day.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentUICulture),
                day.ToString("ddd", CultureInfo.CurrentUICulture),
                day.Day.ToString(CultureInfo.CurrentUICulture),
                groups[i].ToList()));
        }

        _types = items
            .Where(s => s.Type is not null)
            .Select(s => s.Type!.Value)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    private static string Start(PublicSessionListItem session) => EventTime.Time(session.Start);
    private static string End(PublicSessionListItem session) => EventTime.Time(session.End);

    private static string Title(PublicSessionListItem session) =>
        Pick(session.Title, session.TitleArabic);

    private static string Hall(PublicSessionListItem session) =>
        Pick(session.HallName, session.HallNameArabic);

    // The gold topic chip: the session's category, or its primary theme when no
    // category is set; null when neither exists (the chip is then omitted).
    private static string? Chip(PublicSessionListItem session) =>
        PickOrNull(session.CategoryName, session.CategoryNameArabic)
        ?? PickOrNull(session.PrimaryThemeName, session.PrimaryThemeNameArabic);

    // The session abstract shown under the title; null when the API has none.
    private static string? Description(PublicSessionListItem session) =>
        PickOrNull(session.Description, session.DescriptionArabic);

    private static string SpeakerName(PublicSpeakerSummary speaker) =>
        Pick(speaker.Name, speaker.NameArabic);

    // The type-filter slug the card carries and the tab toggles on (invariant
    // enum name, e.g. "Workshop"); empty for an untyped session.
    private static string TypeSlug(PublicSessionListItem session) =>
        session.Type?.ToString() ?? string.Empty;

    // The localized label for a session-type filter tab.
    private string TypeLabel(SessionType type) => L[$"Programme.Type.{type}"];

    private sealed record DaySection(
        string Id,
        string Heading,
        string Weekday,
        string DayNum,
        IReadOnlyList<PublicSessionListItem> Sessions);
}
