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
using SIMF.Contracts.Contacts;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ContactsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "contacts";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminContactSummary> _page = new();
    private bool _loading;
    private Toast? _toast;
    private string _search = string.Empty;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminContactDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    // The contact's logo thumbnail URL, or null so SimfIdentityCell shows an
    // initials tile (never a broken image). Only when HasLogo — the /assets proxy
    // resolves the CompanyLogo StoredFile for this contact (D-357).
    private static string? LogoImageUrl(AdminContactSummary row) =>
        row.HasLogo ? CpAssetUrls.AdminImage(nameof(AssetCategory.CompanyLogo), row.Id) : null;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Contacts.Edit.Title"]
            : L["Admin.Contacts.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Contacts.Delete.Title"]
            : L["Admin.Contacts.Details.Title"],
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

    // The grid summary already carries the resolved country names (server-side),
    // so the cell needs no second lookup.
    private static string CountryCell(AdminContactSummary row)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var name = isArabic ? row.CountryNameAr : row.CountryNameEn;
        return string.IsNullOrWhiteSpace(name) ? "—" : name;
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminContactSummary>>>(
                "simfAccount.postJson", "/account/api/admin/contacts/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Contacts.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task ApplySearchAsync()
    {
        _query.Skip = 0;
        _query.Search = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();
        await LoadAsync();
    }

    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private async Task OnEditAsync(AdminContactSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminContactSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminContactSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // The grid summary omits the logo / social / map fields, so Edit / View /
    // Delete fetch the full detail first (GET /admin/contacts/{id}). Returns
    // null and surfaces a toast on failure.
    private async Task<AdminContactDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminContactDetail>>(
            "simfAccount.getJson", $"/account/api/admin/contacts/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture() ?? L["Admin.Contacts.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminContactSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminContactDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Contacts.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminContactDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Contacts.Deleted"]);
        await LoadAsync();
    }
}
