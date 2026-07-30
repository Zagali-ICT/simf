using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Archive;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ArchiveList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "archive";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminArchiveEditionSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminArchiveEditionSummary? _target;

    private bool _snapshotOpen;
    private bool _snapshotMakeVisible = true;
    private CrudGridExcel? _excel;

    // The edition's cover thumbnail URL, or null so SimfIdentityCell shows an
    // initials tile (never a broken image). Only when HasCover — the /assets proxy
    // resolves the ArchiveCover StoredFile for this edition (D-357).
    private static string? CoverImageUrl(AdminArchiveEditionSummary row) =>
        row.HasCover ? CpAssetUrls.AdminImage(nameof(AssetCategory.ArchiveCover), row.Id) : null;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Archive.Edit.Title"]
            : L["Admin.Archive.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Archive.Delete.Title"]
            : L["Admin.Archive.Details.Title"],
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
        string.Format(L["Admin.Archive.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminArchiveEditionSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/archive/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Archive.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // The grid summary carries every field the Add/Edit + View/Delete forms
    // need (title/summary, counters, cover, place, date label, active), so the
    // forms bind the summary row directly — no per-row detail round-trip
    // (mirrors Interests / ContentBlocks).
    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private Task OnEditAsync(AdminArchiveEditionSummary row)
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = row;
        return Task.CompletedTask;
    }

    private Task OnDetailsAsync(AdminArchiveEditionSummary row)
    {
        _toast = null;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = row;
        return Task.CompletedTask;
    }

    private Task OnDeleteAsync(AdminArchiveEditionSummary row)
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
    private Task OnExportAsync(IReadOnlyList<AdminArchiveEditionSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminArchiveEditionSummary saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Archive.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminArchiveEditionSummary deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Archive.Deleted"]);
        await LoadAsync();
    }

    private void OnSnapshotOpen()
    {
        _snapshotOpen = true;
        _snapshotMakeVisible = true;
        _toast = null;
    }

    private async Task SnapshotConfirmAsync()
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminArchiveEditionDetail>>(
                "simfAccount.postJson",
                "/account/api/admin/archive/snapshot-current",
                new SnapshotCurrentEditionRequest { MakeVisible = _snapshotMakeVisible });
            if (env is { Success: true, Data: not null })
            {
                _snapshotOpen = false;
                _toast = new Toast("success",
                    string.Format(L["Admin.Archive.Snapshot.Done"], env.Data.Year));
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Archive.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
