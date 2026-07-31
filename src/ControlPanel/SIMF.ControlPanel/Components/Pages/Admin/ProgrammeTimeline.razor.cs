using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ProgrammeTimeline
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private sealed record DayGroup(
        string Key, string Heading, IReadOnlyList<AdminSessionSummary> Sessions);

    private bool _loading;
    private Toast? _toast;
    private int _total;
    private List<DayGroup> _days = new();
    private string _selectedDayKey = string.Empty;

    private static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.postJson", "/account/api/admin/sessions/list",
                new GridQuery { Top = 500 });
            if (env is { Success: true, Data: not null })
            {
                BuildDays(env.Data.Items);
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ProgrammeTimeline.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Group by the Saudi calendar day of the start time, days ascending,
    // sessions within a day ascending by start time. Start is a
    // DateTime in UTC;  projects it onto the Saudi (AST)
    // wall clock so the run-of-show reads in local event time.
    private void BuildDays(IReadOnlyList<AdminSessionSummary> items)
    {
        _total = items.Count;
        _days = items
            .OrderBy(s => s.Start)
            .GroupBy(s => s.Start.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DayGroup(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Key.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentUICulture),
                g.ToList()))
            .ToList();
    }

    private IEnumerable<DayGroup> VisibleDays() =>
        string.IsNullOrEmpty(_selectedDayKey)
            ? _days
            : _days.Where(d => d.Key == _selectedDayKey);

    private void OnDayChanged(ChangeEventArgs e) =>
        _selectedDayKey = e.Value?.ToString() ?? string.Empty;

    private static string TimeWindow(AdminSessionSummary s) =>
        $"{s.Start:hh:mm tt} – {s.End:hh:mm tt}";

    private static string SessionTitle(AdminSessionSummary s) =>
        IsArabic ? s.TitleArabic : s.Title;

    private static string HallLabel(AdminSessionSummary s) =>
        IsArabic ? s.HallNameArabic : s.HallName;
}
