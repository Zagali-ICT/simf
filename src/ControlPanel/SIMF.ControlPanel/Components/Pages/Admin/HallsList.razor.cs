using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "halls";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminHallSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminHallDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Halls.Edit.Title"]
            : L["Admin.Halls.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Halls.Delete.Title"]
            : L["Admin.Halls.Details.Title"],
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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
                "simfAccount.postJson", "/account/api/admin/halls/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Halls.LoadFailed"]);
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

    private async Task OnEditAsync(AdminHallSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminHallSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminHallSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // A40 — open the seat-layout editor already focused on THIS hall. The editor
    // reads ?hallId= from the query string, so the admin lands on the hall's grid
    // instead of an empty picker they have to re-find the hall in.
    private void OpenSeatLayout(AdminHallSummary row) =>
        Nav.NavigateTo($"/admin/halls/seat-layouts?hallId={row.Id}");

    // Edit / View / Delete all work against the full detail (the grid carries
    // a summary). Returns null and surfaces a toast on failure.
    private async Task<AdminHallDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var envelope = await JS.InvokeAsync<ApiResult<AdminHallDetail>>(
            "simfAccount.getJson", $"/account/api/admin/halls/{id}");
        if (envelope is { Success: true, Data: not null })
        {
            return envelope.Data;
        }
        _toast = new Toast("error",
            envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Halls.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminHallSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminHallDetail saved)
    {
        var wasEdit = _isEdit;
        CloseForm();
        _toast = new Toast("success",
            string.Format(wasEdit ? L["Admin.Halls.Updated"] : L["Admin.Halls.Created"], saved.Name));
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminHallDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success",
            string.Format(L["Admin.Halls.Deactivated"], deleted.Name));
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Halls.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Halls.Pager.Page"], current, total);
}
