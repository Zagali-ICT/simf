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
using SIMF.Contracts.Organisations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OrganisationsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "organisations";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminOrganisationSummary> _page = new();
    private bool _loading;
    private Toast? _toast;
    private string _search = string.Empty;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminOrganisationDetail? _target;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    // Import-modal state. _importFileName is the picked .xlsx name; the bytes
    // ride the hidden <input> via simfAccount.uploadFile. _importResult holds
    // the server's row counts so they can be shown after the upload.
    private bool _importOpen;
    private bool _importing;
    private string? _importFileName;
    private OrganisationImportResult? _importResult;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Organisations.Edit.Title"]
            : L["Admin.Organisations.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Organisations.Delete.Title"]
            : L["Admin.Organisations.Details.Title"],
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminOrganisationSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/organisations/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Organisations.LoadFailed"]);
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

    private async Task OnEditAsync(AdminOrganisationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminOrganisationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminOrganisationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // The grid summary omits Phone / Email / Website, so Edit / View / Delete
    // all work against the full detail (mirrors GET /admin/organisations/{id}).
    // Returns null and surfaces a toast on failure.
    private async Task<AdminOrganisationDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminOrganisationDetail>>(
            "simfAccount.getJson", $"/account/api/admin/organisations/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Organisations.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    private async Task OnSavedAsync(AdminOrganisationDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Organisations.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminOrganisationDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Organisations.Deleted"]);
        await LoadAsync();
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy; Organisations keeps its bespoke
    // government-Excel import below (a separate hidden input + modal).
    private Task OnExportAsync(IReadOnlyList<AdminOrganisationSummary> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/organisations/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    // -- Excel import --
    private void OpenImport()
    {
        _importOpen = true;
        _importing = false;
        _importFileName = null;
        _importResult = null;
        _toast = null;
    }

    private async Task OnImportFileSelected(ChangeEventArgs _)
    {
        // Capture the picked file name so the Upload button can enable; the
        // bytes ride the hidden <input> via simfAccount.uploadFile.
        var name = await JS.InvokeAsync<string?>(
            "eval", "document.getElementById('organisations-import-input')?.files?.[0]?.name ?? null");
        _importFileName = name;
        _importResult = null;
    }

    private async Task UploadImportAsync()
    {
        if (_importing || string.IsNullOrEmpty(_importFileName)) return;
        _importing = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<OrganisationImportResult>>(
                "simfAccount.uploadFile",
                "/account/api/admin/organisations/import",
                "organisations-import-input");
            if (env is { Success: true, Data: not null })
            {
                _importResult = env.Data;
                _toast = new Toast("success",
                    string.Format(L["Admin.Organisations.Import.Done"],
                        _importResult.Inserted, _importResult.Updated, _importResult.Skipped));
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Organisations.Import.Failed"]);
            }
        }
        finally { _importing = false; }
    }
}
