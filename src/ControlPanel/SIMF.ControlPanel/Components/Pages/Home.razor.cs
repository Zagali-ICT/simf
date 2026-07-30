using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Charts;
using SIMF.Contracts.Statistics;

namespace SIMF.ControlPanel.Components.Pages;

public partial class Home
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IAuthorizationService Authz { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    // Fully-qualified: the component's generated class and the contract share the
    // name StatisticsDashboard (same reason as the statistics page).
    private SIMF.Contracts.Statistics.StatisticsDashboard? _dashboard;
    private StatisticsProgramme? _programme;
    private bool _canViewStats;

    // Derived once per load rather than per render — the chart is pure input.
    private IReadOnlyList<ChartGroup> _chartGroups = [];
    private IReadOnlyList<string> _seriesLabels = [];
    private double _gaugeMax = 1d;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is null) { return; }
        var user = (await AuthState).User;
        _canViewStats = (await Authz.AuthorizeAsync(
            user, PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View))).Succeeded;
        if (_canViewStats)
        {
            await LoadStatsAsync();
            await LoadProgrammeAsync();
            BuildChartModel();
        }
    }

    private async Task LoadStatsAsync()
    {
        // A failure here leaves the dashboard on the welcome panel only — the
        // stat cards are an enhancement, not the page's reason to exist.
        var env = await JS.InvokeAsync<ApiResult<SIMF.Contracts.Statistics.StatisticsDashboard>>(
            "simfAccount.getJson", "/account/api/admin/statistics");
        if (env is { Success: true, Data: not null })
        {
            _dashboard = env.Data;
        }
    }

    private async Task LoadProgrammeAsync()
    {
        // Same tolerance as the stat cards: the two calls are independent, so a
        // failure of one still renders the other.
        var env = await JS.InvokeAsync<ApiResult<StatisticsProgramme>>(
            "simfAccount.getJson", "/account/api/admin/statistics/programme");
        if (env is { Success: true, Data: not null })
        {
            _programme = env.Data;
        }
    }

    /// <summary>
    /// Projects the programme days onto the chart's shape. Only the three
    /// people-metrics go on the chart: they share the unit "people", so one axis
    /// is honest. Sessions-per-day is a much smaller count on a different scale
    /// and is rendered as a number on each day card instead — a second y-axis
    /// would let the reader infer a relationship that is not there.
    /// </summary>
    private void BuildChartModel()
    {
        _seriesLabels =
        [
            L["Dashboard.Series.Registered"],
            L["Dashboard.Series.Present"],
            L["Dashboard.Series.Attended"],
        ];

        if (_programme is null || _programme.Days.Count == 0)
        {
            _chartGroups = [];
            _gaugeMax = 1d;
            return;
        }

        _chartGroups = _programme.Days
            .Select(day => new ChartGroup(
                DayTitle(day),
                [day.Registered, day.Present, day.Attended]))
            .ToList();

        // One shared scale across the day cards, so a gauge on day 1 is directly
        // comparable with the same gauge on day 3.
        _gaugeMax = ChartGeometry.NiceMax(ChartGeometry.MaxValue(_chartGroups));
    }

    private string ProgrammeSubtitle =>
        _programme is null || _programme.Days.Count == 0
            ? string.Empty
            : string.Format(
                CultureInfo.CurrentUICulture,
                L["Dashboard.Programme.Subtitle"],
                _programme.Days.Count);

    /// <summary>The day's own bilingual title, falling back to the English title
    /// when the Arabic one has not been filled in.</summary>
    private static string DayTitle(ProgrammeDayStats day) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            && !string.IsNullOrWhiteSpace(day.TitleArabic)
                ? day.TitleArabic
                : day.Title;

    /// <summary>The programme date. <c>ProgrammeDay.Date</c> is already the
    /// event-local calendar day, so there is no instant to convert — it only
    /// needs the standard Saudi display format.</summary>
    private static string DayDate(ProgrammeDayStats day) =>
        day.Date.ToString(SaudiTime.DateFormat, CultureInfo.InvariantCulture);

    private static string Count(int value) => value.ToString("#,##0", CultureInfo.CurrentUICulture);

    private static string Average(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);
}
