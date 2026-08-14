using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>
/// VIP Registration desk (/admin/visitors/vip) — the VVIP/VIP management list.
/// A copy of the visitor list scoped to the VIP guests (the /admin/vips/list
/// subset: ProfileType.Name in {VVIP, VIP, Gold}). "New VIP" opens the VVIP/VIP
/// registration wizard; the per-row Edit opens the shared account edit form
/// (name / email / tier / photo / ID / welcome photo). No new backend endpoint —
/// it reuses the existing VIP list + the account-id-keyed admin edit/upload
/// endpoints, all Visitors.Edit-gated.
/// </summary>
public partial class VipRegistration
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IAuthorizationService Authz { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthState { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminVipSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    // UX gates — New VIP shows for admins holding Visitors.RegisterOnsite, Edit
    // for Visitors.Edit. The API enforces the same policies.
    private bool _canRegister;
    private bool _canEdit;

    /// <summary>The VIP being read. Unconditional, unlike Add and Edit
    /// above: the page gate already decided who may read this list, and the row
    /// the grid holds carries the Arabic name and job title no column shows.</summary>
    private AdminVipSummary? _vipDetails;

    private void OnVipDetails(AdminVipSummary vip) => _vipDetails = vip;

    private bool _addOpen;
    private bool _editOpen;
    private Guid _editUserId;

    protected override async Task OnInitializedAsync()
    {
        var user = (await AuthState).User;
        _canRegister = (await Authz.AuthorizeAsync(
            user, PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite))).Succeeded;
        _canEdit = (await Authz.AuthorizeAsync(
            user, PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit))).Succeeded;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminVipSummary>>>(
                "simfAccount.postJson", "/account/api/admin/vips/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Vips.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    // New VIP — show the VVIP/VIP registration wizard section.
    private void OnAddVip() => _addOpen = true;

    private async Task OnAddedAsync(AdminCreateUserResponse _)
    {
        _addOpen = false;
        _toast = new Toast("success", L["Admin.VipRegistration.Added"]);
        await LoadAsync();
    }

    // Row Edit — open the shared account edit form for this VIP.
    //
    // The form edits an ACCOUNT, and a guest invited through a bulk order or
    // registered at a desk may not have one. The grid offers Edit per row with no
    // way to disable a single row's button, so say why instead of opening a form
    // bound to nothing: an empty form that silently saves nowhere is the worse
    // failure, and the guest is on the list either way.
    private void OnEditVip(AdminVipSummary row)
    {
        if (row.UserId is not { } accountId)
        {
            _toast = new Toast("info", L["Admin.Vips.Edit.NoAccount"]);
            return;
        }
        _editUserId = accountId;
        _editOpen = true;
    }

    private async Task OnEditSavedAsync()
    {
        _editOpen = false;
        _toast = new Toast("success", L["Admin.Vips.Edit.Saved"]);
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private static string RecipientLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ArabicName
            : row.EnglishName;

    private static string TypeLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ProfileTypeNameArabic
            : row.ProfileTypeName;
}
