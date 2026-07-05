using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Contracts.Programme;

namespace SIMF.Web.Components.Pages;

// Website — public programme / agenda page (D-199, Mockup page 16 "Agenda").
// Static-SSR public read over the anonymous backend; groups the published
// sessions by the local calendar date of StartUtc into day sections and shows
// an optional best-effort speakers strip. Markup lives in Programme.razor.
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

    // Group sessions by the local calendar date of StartUtc, ordered by day
    // and then by start time within each day.
    private void BuildDays(IReadOnlyList<PublicSessionListItem> items)
    {
        var groups = items
            .OrderBy(s => s.StartUtc)
            .GroupBy(s => s.StartUtc.ToLocalTime().Date)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            _days.Add(new DaySection(
                group.Key.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentUICulture),
                group.ToList()));
        }
    }

    // Local time window, e.g. "09:00 – 10:30", in the current culture.
    private static string TimeWindow(PublicSessionListItem session)
    {
        var start = session.StartUtc.ToLocalTime();
        var end = session.EndUtc.ToLocalTime();
        var pattern = "HH:mm";
        return $"{start.ToString(pattern, CultureInfo.CurrentUICulture)} – " +
               $"{end.ToString(pattern, CultureInfo.CurrentUICulture)}";
    }

    private static bool PreferArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    private static string Title(PublicSessionListItem session) =>
        Pick(session.TitleArabic, session.Title);

    private static string Hall(PublicSessionListItem session) =>
        Pick(session.HallNameArabic, session.HallName);

    private static string ThemeName(PublicSessionListItem session) =>
        Pick(session.PrimaryThemeNameArabic, session.PrimaryThemeName);

    private static string SpeakerName(PublicSpeakerSummary speaker) =>
        Pick(speaker.NameArabic, speaker.Name);

    // Arabic-preferred-then-English fallback: in an Arabic UI use the Arabic
    // value when present, otherwise fall back to the base (English) value;
    // a null base renders as empty.
    private static string Pick(string? arabic, string? @base)
    {
        if (PreferArabic && !string.IsNullOrWhiteSpace(arabic))
        {
            return arabic;
        }
        return @base ?? string.Empty;
    }

    private sealed record DaySection(string Heading, IReadOnlyList<PublicSessionListItem> Sessions);
}
