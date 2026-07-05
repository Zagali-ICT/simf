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
using SIMF.Contracts.Regions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RegionsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "regions";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminRegionSummary> _page = new();
    private bool _loading;
    private Toast? _toast;
    private string _search = string.Empty;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminRegionDetail? _target;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Regions.Edit.Title"]
            : L["Admin.Regions.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Regions.Delete.Title"]
            : L["Admin.Regions.Details.Title"],
        _ => string.Empty,
    };

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
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

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminRegionSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/regions/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Regions.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Search reloads the grid server-side via GridQuery.Search, resetting to
    // the first page so the result set starts at the top.
    private async Task ApplySearchAsync()
    {
        _query.Skip = 0;
        _query.Search = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();
        await LoadAsync();
    }

    private void OnAdd()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
    }

    private async Task OnEditAsync(AdminRegionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminRegionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminRegionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // The grid summary omits SortOrder / timestamps, so Edit / View / Delete
    // all work against the full detail (mirrors GET /admin/regions/{id}).
    // Returns null and surfaces a toast on failure.
    private async Task<AdminRegionDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminRegionDetail>>(
            "simfAccount.getJson", $"/account/api/admin/regions/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Regions.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    private async Task OnSavedAsync(AdminRegionDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Regions.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminRegionDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Regions.Deleted"]);
        await LoadAsync();
    }
}
