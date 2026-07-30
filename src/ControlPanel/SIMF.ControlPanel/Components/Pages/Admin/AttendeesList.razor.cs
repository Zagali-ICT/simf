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

public partial class AttendeesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 25 };
    private GridPage<AdminAttendeeSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private string _userTypeFilter = string.Empty;
    private string _accountStateFilter = string.Empty;
    private string _searchFilter = string.Empty;
    private string _fromFilter = string.Empty;
    private string _toFilter = string.Empty;

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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminAttendeeSummary>>>(
                "simfAccount.postJson", "/account/api/admin/attendees/list",
                ApplyFilterValues(_query));
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Attendees.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private GridQuery ApplyFilterValues(GridQuery source)
    {
        var filters = new Dictionary<string, string>(source.Filters,
            StringComparer.OrdinalIgnoreCase);
        SetOrRemove(filters, "userType", _userTypeFilter);
        SetOrRemove(filters, "accountState", _accountStateFilter);
        // The "to" bound is made inclusive of the whole picked day; "from"
        // is start-of-day as the date input already gives it.
        SetOrRemove(filters, "from", _fromFilter);
        SetOrRemove(filters, "to",
            string.IsNullOrWhiteSpace(_toFilter) ? string.Empty : _toFilter + "T23:59:59");
        source.Filters = filters;
        source.Search = string.IsNullOrWhiteSpace(_searchFilter) ? null : _searchFilter.Trim();
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
        _userTypeFilter = string.Empty;
        _accountStateFilter = string.Empty;
        _searchFilter = string.Empty;
        _fromFilter = string.Empty;
        _toFilter = string.Empty;
        _query.Skip = 0;
        _query.Search = null;
        _query.Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await LoadAsync();
    }

    // P1.6 — download the filtered roster as XLSX. The browser saves the file;
    // the BFF streams the workbook bytes (no ApiResult envelope).
    private async Task OnExportAsync()
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/attendees/export", ApplyFilterValues(_query), L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private string LocaliseUserType(string raw) => raw switch
    {
        "Visitor" => L["Admin.Attendees.UserType.Visitor"],
        "Other" => L["Admin.Attendees.UserType.Other"],
        _ => raw,
    };

    private string? ProfileTypeLabel(AdminAttendeeSummary row)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return isArabic ? row.ProfileTypeNameArabic : row.ProfileTypeName;
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Attendees.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Attendees.Pager.Page"], current, total);
}
