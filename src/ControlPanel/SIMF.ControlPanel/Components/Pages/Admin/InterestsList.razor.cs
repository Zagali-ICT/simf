using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class InterestsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "interests";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminInterestSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminInterestSummary? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Interests.Edit.Title"]
            : L["Admin.Interests.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Interests.Delete.Title"]
            : L["Admin.Interests.Details.Title"],
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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminInterestSummary>>>(
                "simfAccount.postJson", "/account/api/admin/interests/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Interests.LoadFailed"]);
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

    private Task OnEditAsync(AdminInterestSummary row)
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = row;
        return Task.CompletedTask;
    }

    private Task OnDetailsAsync(AdminInterestSummary row)
    {
        _toast = null;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = row;
        return Task.CompletedTask;
    }

    private Task OnDeleteAsync(AdminInterestSummary row)
    {
        _toast = null;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = row;
        return Task.CompletedTask;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminInterestSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminInterestSummary saved)
    {
        var wasEdit = _isEdit;
        CloseForm();
        _toast = new Toast("success",
            string.Format(wasEdit ? L["Admin.Interests.Updated"] : L["Admin.Interests.Created"], saved.Name));
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminInterestSummary deleted)
    {
        CloseForm();
        _toast = new Toast("success",
            string.Format(L["Admin.Interests.Deactivated"], deleted.Name));
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Interests.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Interests.Pager.Page"], current, total);
}
