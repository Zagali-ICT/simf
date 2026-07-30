using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.PublicRelations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaPartnersList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "media-partners";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminMediaPartnerSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminMediaPartnerDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.MediaPartners.Edit.Title"]
            : L["Admin.MediaPartners.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.MediaPartners.Delete.Title"]
            : L["Admin.MediaPartners.Details.Title"],
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
        string.Format(L["Admin.MediaPartners.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // The identity cell shows the real logo only when an active MediaPartnerLogo
    // asset exists (HasLogo); a null URL falls back to an initials tile.
    private static string? LogoImageUrl(AdminMediaPartnerSummary row) =>
        row.HasLogo ? CpAssetUrls.AdminImage(nameof(AssetCategory.MediaPartnerLogo), row.Id) : null;

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminMediaPartnerSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/media-partners/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.MediaPartners.LoadFailed"]);
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

    private async Task OnEditAsync(AdminMediaPartnerSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminMediaPartnerSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminMediaPartnerSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail — the grid summary
    // omits ContactId (SIMF-FDS-014 / D-283), so editing from a summary-only
    // form would wipe an existing link. Returns null and surfaces a toast on
    // failure.
    private async Task<AdminMediaPartnerDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminMediaPartnerDetail>>(
            "simfAccount.getJson", $"/account/api/admin/media-partners/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.MediaPartners.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminMediaPartnerSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminMediaPartnerDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.MediaPartners.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminMediaPartnerDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.MediaPartners.Deleted"]);
        await LoadAsync();
    }
}
