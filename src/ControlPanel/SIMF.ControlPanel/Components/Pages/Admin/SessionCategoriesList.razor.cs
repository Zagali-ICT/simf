using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionCategoriesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "session-categories";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSessionCategorySummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminSessionCategoryDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.SessionCategories.Edit.Title"]
            : L["Admin.SessionCategories.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.SessionCategories.Delete.Title"]
            : L["Admin.SessionCategories.Details.Title"],
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

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionCategorySummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/session-categories/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionCategories.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private async Task OnEditAsync(AdminSessionCategorySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminSessionCategorySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminSessionCategorySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (the grid carries
    // a summary). Returns null and surfaces a toast on failure.
    private async Task<AdminSessionCategoryDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var envelope = await JS.InvokeAsync<ApiResult<AdminSessionCategoryDetail>>(
            "simfAccount.getJson", $"/account/api/admin/session-categories/{id}");
        if (envelope is { Success: true, Data: not null })
        {
            return envelope.Data;
        }
        _toast = new Toast("error",
            envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.SessionCategories.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminSessionCategorySummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminSessionCategoryDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.SessionCategories.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminSessionCategoryDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.SessionCategories.Deleted"]);
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.SessionCategories.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);
}
