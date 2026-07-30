using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class CountriesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "countries";

    private GridQuery _query = new() { Top = 50 };
    private GridPage<AdminCountrySummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminCountryDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Countries.Edit.Title"]
            : L["Admin.Countries.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Countries.Delete.Title"]
            : L["Admin.Countries.Details.Title"],
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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminCountrySummary>>>(
                "simfAccount.postJson", "/account/api/admin/countries/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Countries.LoadFailed"]);
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

    private async Task OnEditAsync(AdminCountrySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminCountrySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminCountrySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (the grid carries
    // a summary). Returns null and surfaces a toast on failure.
    private async Task<AdminCountryDetail?> LoadDetailAsync(int id)
    {
        _toast = null;
        var envelope = await JS.InvokeAsync<ApiResult<AdminCountryDetail>>(
            "simfAccount.getJson", $"/account/api/admin/countries/{id}");
        if (envelope is { Success: true, Data: not null })
        {
            return envelope.Data;
        }
        _toast = new Toast("error",
            envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Countries.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    // Country ids are int (ISO 3166-1 numeric), not the Guid the generic export
    // contract carries, so export always sends the current filtered set (empty
    // ids) rather than a per-row selection.
    private Task OnExportAsync(IReadOnlyList<AdminCountrySummary> selected) =>
        _excel!.ExportAsync(Array.Empty<Guid>(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminCountryDetail saved)
    {
        var wasEdit = _isEdit;
        CloseForm();
        _toast = new Toast("success",
            string.Format(wasEdit ? L["Admin.Countries.Updated"] : L["Admin.Countries.Created"], saved.Name));
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminCountryDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success",
            string.Format(L["Admin.Countries.Deactivated"], deleted.Name));
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Countries.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Countries.Pager.Page"], current, total);
}
