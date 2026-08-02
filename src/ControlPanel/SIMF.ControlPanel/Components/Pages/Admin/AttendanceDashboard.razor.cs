using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Attendance;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AttendanceDashboard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private SessionAttendanceSummary? _summary;
    private GridQuery _query = new() { Top = 20, Sort = "start" };
    private GridPage<SessionAttendanceRow> _page = new();
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        // Flip the grid into its loading state from the very first paint:
        // OnInitializedAsync first yields at the summary fetch below, so without
        // this the in-progress frame would render with _loading == false and the
        // spinner would never show on first load. LoadAsync's finally clears it.
        _loading = true;
        await LoadSummaryAsync();
        await LoadAsync();
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private static string Count(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private async Task LoadSummaryAsync()
    {
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SessionAttendanceSummary>>(
                "simfAccount.getJson",
                "/account/api/admin/attendance/summary");
            if (env is { Success: true, Data: not null })
            {
                _summary = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Attendance.LoadFailed"]);
            }
        }
        catch
        {
            _toast = new Toast("error", L["Admin.Attendance.LoadFailed"]);
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<SessionAttendanceRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/attendance/sessions/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Attendance.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }
}
