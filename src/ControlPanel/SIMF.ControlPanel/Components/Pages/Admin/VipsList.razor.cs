using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
using SIMF.Contracts.Ai;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VipsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IAuthorizationService Authz { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState> AuthState { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminVipSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private readonly HashSet<Guid> _selected = new();

    private bool _notifyOpen;
    private string _msgTitle = string.Empty;
    private string _msgTitleArabic = string.Empty;
    private string _msgBody = string.Empty;
    private string _msgBodyArabic = string.Empty;

    // UX gates — the "New VIP" and per-row "Edit" affordances only show for admins
    // who actually hold the underlying visitor permissions (the API enforces the
    // same policies). The VIP page itself is Vips.View, which is a lower bar.
    private bool _canRegister;
    private bool _canEdit;

    // Edit state — the row Edit opens a modal hosting the shared EditAccountForm
    // keyed by the account id (AdminVipSummary.UserId), scope "visitors" (VIP is a
    // visitor tier), with the VIP welcome photo field shown.
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
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Vips.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OnSelectionChanged(IReadOnlySet<string> keys)
    {
        _selected.Clear();
        foreach (var key in keys)
        {
            if (Guid.TryParse(key, out var id)) { _selected.Add(id); }
        }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — the VIP list is a
    // derived view (no add/edit/import); the page's only action is bulk-notify.
    // The row id is the UserProfileId (the grid's row key).
    private Task OnExportAsync(IReadOnlyList<AdminVipSummary> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/vips/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.UserProfileId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // "New VIP" — reuse the dedicated VVIP/VIP registration page (the picker is
    // restricted to VVIP/VIP there and it captures the موج welcome photo).
    private void OnAddVip() => Nav.NavigateTo("/admin/visitors/vip");

    // Row Edit — open the shared account edit form for this VIP (change name,
    // email, tier, photo, ID, and the VIP welcome photo).
    private void OnEditVip(AdminVipSummary row)
    {
        _editUserId = row.UserId;
        _editOpen = true;
    }

    private async Task OnEditSavedAsync()
    {
        _editOpen = false;
        _toast = new Toast("success", L["Admin.Vips.Edit.Saved"]);
        await LoadAsync();
    }

    private void OnNotifySelected()
    {
        if (_selected.Count == 0) return;
        _notifyOpen = true;
        _msgTitle = string.Empty;
        _msgTitleArabic = string.Empty;
        _msgBody = string.Empty;
        _msgBodyArabic = string.Empty;
    }

    private async Task SubmitNotifyAsync()
    {
        if (_busy || _selected.Count == 0) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminNotifyVipsResult>>(
                "simfAccount.postJson", "/account/api/admin/vips/notify",
                new AdminNotifyVipsRequest
                {
                    UserProfileIds = _selected.ToList(),
                    Title = _msgTitle,
                    TitleArabic = _msgTitleArabic,
                    Body = _msgBody,
                    BodyArabic = _msgBodyArabic,
                });
            if (env is { Success: true, Data: not null })
            {
                _notifyOpen = false;
                _toast = new Toast("success",
                    string.Format(L["Admin.Vips.Notify.Sent"],
                        env.Data.Dispatched, env.Data.EmailsEnqueued));
                _selected.Clear();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Vips.Notify.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private static string RecipientLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ArabicName
            : row.EnglishName;

    private static string TypeLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ProfileTypeNameArabic
            : row.ProfileTypeName;
}
