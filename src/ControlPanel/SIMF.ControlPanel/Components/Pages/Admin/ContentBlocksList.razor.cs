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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ContentBlocksList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "content-blocks";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminContentBlockSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminContentBlockSummary? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.ContentBlocks.Edit.Title"]
            : L["Admin.ContentBlocks.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.ContentBlocks.Delete.Title"]
            : L["Admin.ContentBlocks.Details.Title"],
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
        string.Format(L["Admin.ContentBlocks.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminContentBlockSummary>>>(
                "simfAccount.postJson", "/account/api/admin/content-blocks/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ContentBlocks.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // The grid summary already carries every field the forms need (Key,
    // Content, ContentArabic, IsActive) â€” so the row is bound straight to the
    // form. No detail-fetch (like Interests / the upsert-by-Key contract).
    private void OnAdd()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
    }

    private void OnEdit(AdminContentBlockSummary row)
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = row;
    }

    private void OnDetails(AdminContentBlockSummary row)
    {
        _toast = null;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = row;
    }

    private void OnDelete(AdminContentBlockSummary row)
    {
        _toast = null;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = row;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminContentBlockSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminContentBlockSummary saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.ContentBlocks.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminContentBlockSummary deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.ContentBlocks.Deleted"]);
        await LoadAsync();
    }

    private static string TruncatePreview(string s) =>
        s.Length > 80 ? s.Substring(0, 80) + "â€¦" : s;
}
