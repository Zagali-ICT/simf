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

public partial class UsersAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Issue-1 — the assignable CP roles (fetched) and the user's current selection.
    private readonly List<string> _availableRoles = new();
    private readonly HashSet<string> _editSelectedRoles = new(StringComparer.Ordinal);
    private bool _editLoading;
    private bool _editBusy;
    private string? _editError;

    protected override async Task OnInitializedAsync()
    {
        // Only Edit mode loads roles; Add mode is entirely handled by CreateAdminForm.
        if (!IsEdit || Initial is null) { return; }

        _editError = null;
        _editLoading = true;
        _availableRoles.Clear();
        _editSelectedRoles.Clear();
        try
        {
            var roles = await JS.InvokeAsync<ApiResult<GridPage<AdminRoleSummary>>>(
                "simfAccount.postJson", "/account/api/admin/roles/list",
                new GridQuery { Top = 200, Sort = "name" });
            if (roles is { Success: true, Data: not null })
            {
                _availableRoles.AddRange(roles.Data.Items.Select(role => role.Name));
            }

            var current = await JS.InvokeAsync<ApiResult<AdminUserRolesResponse>>(
                "simfAccount.getJson", $"/account/api/admin/admins/{Initial.Id}/roles");
            if (current is { Success: true, Data: not null })
            {
                foreach (var roleName in current.Data.Roles)
                {
                    _editSelectedRoles.Add(roleName);
                }
            }
        }
        catch (JSException)
        {
            _editError = L["Admin.Users.EditRoles.LoadFailed"];
        }
        finally { _editLoading = false; }
    }

    // Add — bridge CreateAdminForm's response back to the standard CRUD surface.
    // The host's OnSavedAsync only needs the row identity (it reloads the grid);
    // in Add mode there is no Initial, so it passes null, matching the base type.
    private Task OnCreatedAsync(AdminCreateUserResponse created) =>
        OnSuccess.InvokeAsync(Initial);

    private void ToggleEditRole(string roleName, bool on)
    {
        if (on) { _editSelectedRoles.Add(roleName); }
        else { _editSelectedRoles.Remove(roleName); }
    }

    private async Task SaveRolesAsync()
    {
        if (Initial is null || _editBusy) { return; }
        _editBusy = true;
        _editError = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson", $"/account/api/admin/admins/{Initial.Id}/roles",
                new AdminSetUserRolesRequest { Roles = _editSelectedRoles.ToList() });
            if (envelope is { Success: true })
            {
                await OnSuccess.InvokeAsync(Initial);
            }
            else
            {
                _editError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.EditRoles.Fallback"];
            }
        }
        finally { _editBusy = false; }
    }
}
