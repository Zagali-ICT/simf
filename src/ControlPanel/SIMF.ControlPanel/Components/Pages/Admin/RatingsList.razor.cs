using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Logs;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Feedback;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RatingsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminRatingResponseSummary> _page = new();
    private double _averageOverall;
    private int _responseCount;
    private AdminRatingKpiView? _kpi;
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await LoadKpiAsync();
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

    // Excel export (selected rows, or the current filtered set). Export only —
    // responses are owned by the attendees who submit them (read-only view).
    private async Task OnExportAsync(IReadOnlyList<AdminRatingResponseSummary> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/ratings/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminRatingResponsesPage>>(
                "simfAccount.postJson",
                "/account/api/admin/feedback/ratings", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data.Responses;
                _averageOverall = env.Data.AverageOverall;
                _responseCount = env.Data.ResponseCount;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Ratings.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task LoadKpiAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<AdminRatingKpiView>>(
            "simfAccount.getJson", "/account/api/admin/feedback/ratings/kpi");
        if (env is { Success: true, Data: not null }) { _kpi = env.Data; }
    }
}
