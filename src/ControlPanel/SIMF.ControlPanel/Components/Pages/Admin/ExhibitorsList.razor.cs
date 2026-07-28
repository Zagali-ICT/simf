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
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ExhibitorsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "exhibitors";

    private sealed class ProvisionModel
    {
        public string ContactName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
    }

    // D-781 — the "attach an EXISTING account" form. Only the email is required;
    // the contact name defaults to the account's display name server-side.
    private sealed class LinkModel
    {
        public string Email { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
    }

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminExhibitorSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;

    // The exhibitor's own logo thumbnail URL — the exhibitor's ExhibitorLogo asset
    // (owner = the exhibitor, the same logo the app renders), or null so
    // SimfIdentityCell shows an initials tile (never a broken image).
    private static string? LogoImageUrl(AdminExhibitorSummary row) =>
        row.HasExhibitorLogo
            ? CpAssetUrls.AdminImage(nameof(AssetCategory.ExhibitorLogo), row.Id)
            : null;
    private AdminExhibitorDetail? _target;
    private CrudGridExcel? _excel;

    private bool _accountsOpen;
    private bool _accountsLoading;
    private Guid _accountsExhibitorId;
    private string _accountsExhibitorName = string.Empty;
    private List<ExhibitorAccountSummary> _accounts = new();
    private bool _provisionBusy;
    private ProvisionModel _provision = new();
    private bool _linkBusy;
    private LinkModel _link = new();

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Exhibitors.Edit.Title"]
            : L["Admin.Exhibitors.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Exhibitors.Delete.Title"]
            : L["Admin.Exhibitors.Details.Title"],
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminExhibitorSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/exhibitors/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.LoadFailed"]);
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

    private async Task OnEditAsync(AdminExhibitorSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminExhibitorSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminExhibitorSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (the grid carries a
    // summary that omits the ContactId / timestamps). Returns null and surfaces
    // a toast on failure.
    private async Task<AdminExhibitorDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminExhibitorDetail>>(
            "simfAccount.getJson", $"/account/api/admin/exhibitors/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Exhibitors.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminExhibitorSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminExhibitorDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Exhibitors.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminExhibitorDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Exhibitors.Deleted"]);
        await LoadAsync();
    }

    private async Task OnAccountsAsync(AdminExhibitorSummary row)
    {
        _accountsOpen = true;
        _accountsExhibitorId = row.Id;
        _accountsExhibitorName = row.NameEn;
        _provision = new ProvisionModel();
        _link = new LinkModel();
        _toast = null;
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        _accountsLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<IReadOnlyList<ExhibitorAccountSummary>>>(
                "simfAccount.getJson",
                $"/account/api/admin/exhibitors/{_accountsExhibitorId}/accounts");
            if (env is { Success: true, Data: not null })
            {
                _accounts = env.Data.ToList();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.Accounts.LoadFailed"]);
            }
        }
        finally { _accountsLoading = false; }
    }

    private async Task ProvisionAsync()
    {
        if (_provisionBusy) return;
        if (string.IsNullOrWhiteSpace(_provision.ContactName)
            || string.IsNullOrWhiteSpace(_provision.Email))
        {
            _toast = new Toast("error", L["Admin.Exhibitors.Provision.Required"]);
            return;
        }
        _provisionBusy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<ExhibitorAccountSummary>>(
                "simfAccount.postJson",
                $"/account/api/admin/exhibitors/{_accountsExhibitorId}/accounts",
                new ProvisionExhibitorAccountRequest
                {
                    ContactName = _provision.ContactName,
                    Email = _provision.Email,
                    RoleLabel = NullIfBlank(_provision.RoleLabel),
                });
            if (env is { Success: true })
            {
                _provision = new ProvisionModel();
                _toast = new Toast("success", L["Admin.Exhibitors.Provision.Done"]);
                await LoadAccountsAsync();
                // Refresh the grid so the AccountCount column reflects the new account.
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.Provision.Failed"]);
            }
        }
        finally { _provisionBusy = false; }
    }

    // D-781 — attach an EXISTING account to this exhibitor. The account must
    // already carry an exhibitor profile type (set on the Others page); the API
    // answers 409 EXHIBITOR_ACCOUNT_NOT_ELIGIBLE with a bilingual message if not,
    // which is surfaced verbatim.
    private async Task LinkAsync()
    {
        if (_linkBusy) return;
        if (string.IsNullOrWhiteSpace(_link.Email))
        {
            _toast = new Toast("error", L["Admin.Exhibitors.Link.Required"]);
            return;
        }
        _linkBusy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<ExhibitorAccountSummary>>(
                "simfAccount.postJson",
                $"/account/api/admin/exhibitors/{_accountsExhibitorId}/accounts/link",
                new LinkExhibitorAccountRequest
                {
                    Email = _link.Email,
                    ContactName = NullIfBlank(_link.ContactName),
                    RoleLabel = NullIfBlank(_link.RoleLabel),
                });
            if (env is { Success: true })
            {
                _link = new LinkModel();
                _toast = new Toast("success", L["Admin.Exhibitors.Link.Done"]);
                await LoadAccountsAsync();
                // Refresh the grid so the AccountCount column reflects the link.
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.Link.Failed"]);
            }
        }
        finally { _linkBusy = false; }
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
