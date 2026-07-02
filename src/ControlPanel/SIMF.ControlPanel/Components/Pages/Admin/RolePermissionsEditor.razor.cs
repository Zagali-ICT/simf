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
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RolePermissionsEditor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public Guid RoleId { get; set; }

    private record Toast(string Variant, string Message);

    private static readonly IReadOnlyList<IGrouping<string, PermissionDef>> _groups =
        PermissionCatalog.All.GroupBy(permission => permission.Page).ToList();

    private AdminRolePermissionsResponse? _role;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private bool _loading = true;
    private bool _busy;
    private Toast? _toast;

    private string _pageTitle = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _pageTitle = L["Admin.RolePermissions.Title"];
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminRolePermissionsResponse>>(
                "simfAccount.getJson", $"/account/api/admin/roles/{RoleId}/permissions");
            if (envelope is { Success: true, Data: not null })
            {
                _role = envelope.Data;
                _selected.Clear();
                foreach (var code in _role.GrantedCodes)
                {
                    _selected.Add(code);
                }
                _pageTitle = string.Format(L["Admin.RolePermissions.TitleFor"], _role.RoleName);
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.RolePermissions.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void Toggle(string code, bool on)
    {
        if (on) { _selected.Add(code); }
        else { _selected.Remove(code); }
    }

    private async Task SaveAsync()
    {
        if (_busy || _role is null || _role.IsBaseline) { return; }

        _busy = true;
        _toast = null;
        try
        {
            var body = new AdminSetRolePermissionsRequest { Codes = _selected.ToList() };
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson", $"/account/api/admin/roles/{RoleId}/permissions", body);
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.RolePermissions.Saved"]);
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.RolePermissions.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private void BackToList() => Nav.NavigateTo("/admin/roles");
}
