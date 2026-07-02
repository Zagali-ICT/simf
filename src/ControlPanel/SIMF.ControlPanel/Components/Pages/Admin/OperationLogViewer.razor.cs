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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OperationLogViewer
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 25 };
    private GridPage<AdminOperationLogSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private string _eventTypeFilter = string.Empty;
    private string _subjectEmailFilter = string.Empty;
    private string _outcomeFilter = string.Empty;
    private string _fromFilter = string.Empty;
    private string _toFilter = string.Empty;

    private AdminOperationLogSummary? _detailsTarget;
    private AdminOperationLogDetail? _detailsDetail;
    private bool _detailsLoading;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = ApplyFilterValues(next);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminOperationLogSummary>>>(
                "simfAccount.postJson", "/account/api/admin/operation-log/list",
                ApplyFilterValues(_query));
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.OperationLog.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private GridQuery ApplyFilterValues(GridQuery source)
    {
        var filters = new Dictionary<string, string>(source.Filters,
            StringComparer.OrdinalIgnoreCase);
        SetOrRemove(filters, "eventType", _eventTypeFilter);
        SetOrRemove(filters, "subjectEmail", _subjectEmailFilter);
        SetOrRemove(filters, "outcome", _outcomeFilter);
        // The "to" bound is made inclusive of the whole picked day; "from"
        // is start-of-day as the date input already gives it.
        SetOrRemove(filters, "from", _fromFilter);
        SetOrRemove(filters, "to",
            string.IsNullOrWhiteSpace(_toFilter) ? string.Empty : _toFilter + "T23:59:59");
        source.Filters = filters;
        return source;
    }

    private static void SetOrRemove(IDictionary<string, string> filters,
        string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            filters.Remove(key);
        }
        else
        {
            filters[key] = value.Trim();
        }
    }

    private async Task ApplyFiltersAsync()
    {
        _query.Skip = 0;
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _eventTypeFilter = string.Empty;
        _subjectEmailFilter = string.Empty;
        _outcomeFilter = string.Empty;
        _fromFilter = string.Empty;
        _toFilter = string.Empty;
        _query.Skip = 0;
        _query.Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await LoadAsync();
    }

    // P1.6 — download the filtered result set as XLSX. The browser saves the
    // file; the BFF streams the workbook bytes (no ApiResult envelope).
    private async Task OnExportAsync() =>
        await JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/operation-log/export", ApplyFilterValues(_query));

    private async Task OnDetailsAsync(AdminOperationLogSummary row)
    {
        _detailsTarget = row;
        _detailsDetail = null;
        _detailsLoading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminOperationLogDetail>>(
                "simfAccount.getJson",
                $"/account/api/admin/operation-log/{row.Id}");
            if (envelope is { Success: true, Data: not null })
            {
                _detailsDetail = envelope.Data;
            }
        }
        finally { _detailsLoading = false; }
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.OperationLog.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.OperationLog.Pager.Page"], current, total);
}
