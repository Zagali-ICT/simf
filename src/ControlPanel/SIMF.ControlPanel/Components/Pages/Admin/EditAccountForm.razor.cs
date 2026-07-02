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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class EditAccountForm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The account being edited.</summary>
    [Parameter, EditorRequired] public Guid AccountId { get; set; }

    /// <summary>"visitors" or "others" — drives the route + scope.</summary>
    [Parameter, EditorRequired] public string Scope { get; set; } = "visitors";

    /// <summary>True for the audience (Visitor) desk; false for the partner (Other) desk.</summary>
    [Parameter] public bool IsVisitorScope { get; set; } = true;

    /// <summary>Raised after a successful save.</summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    /// <summary>Raised when the admin cancels.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private string _email = string.Empty;
    private string _displayName = string.Empty;
    private Guid? _profileTypeId;
    private IReadOnlyList<AdminProfileTypeSummary> _profileTypes = new List<AdminProfileTypeSummary>();

    private bool _loading = true;
    private bool _busy;
    private string? _loadError;
    private string? _saveError;

    private AdminProfileTypeSummary? _selectedProfileType =>
        _profileTypes.FirstOrDefault(p => p.Id == _profileTypeId);

    private bool CanSave =>
        !_busy
        && !string.IsNullOrWhiteSpace(_email)
        && _displayName.Trim().Length >= 2
        && (IsVisitorScope || _profileTypeId is not null);

    private void OnProfileTypeChanged(AdminProfileTypeSummary? selected) =>
        _profileTypeId = selected?.Id;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;
        try
        {
            await LoadProfileTypesAsync();
            await LoadAccountAsync();
        }
        catch (Exception)
        {
            _loadError = L["Admin.Edit.LoadFailed"];
        }
        finally { _loading = false; }
    }

    private async Task LoadProfileTypesAsync()
    {
        // Both Visitor and Other profile types are UserType=Visitor post-D-186;
        // the picker route filters by UserType, then we narrow to the scope's
        // IsVisitor side so the dropdown only offers valid tiers.
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        if (envelope is { Success: true, Data: not null })
        {
            _profileTypes = envelope.Data
                .Where(p => p.IsActive && p.IsVisitor == IsVisitorScope)
                .ToList();
        }
    }

    private async Task LoadAccountAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<AdminUserProfileView>>(
            "simfAccount.getJson", $"/account/api/admin/{Scope}/{AccountId}/profile");
        if (envelope is { Success: true, Data: not null })
        {
            _email = envelope.Data.Email;
            _displayName = envelope.Data.DisplayName;
            _profileTypeId = envelope.Data.ProfileTypeId;
        }
        else
        {
            _loadError = envelope?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Edit.LoadFailed"];
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _busy = true;
        _saveError = null;
        try
        {
            object body = IsVisitorScope
                ? new AdminUpdateVisitorRequest
                {
                    Email = _email.Trim(),
                    DisplayName = _displayName.Trim(),
                    ProfileTypeId = _profileTypeId,
                }
                : new AdminUpdateOtherRequest
                {
                    Email = _email.Trim(),
                    DisplayName = _displayName.Trim(),
                    ProfileTypeId = _profileTypeId ?? Guid.Empty,
                };

            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson", $"/account/api/admin/{Scope}/{AccountId}", body);
            if (envelope is { Success: true })
            {
                await OnSaved.InvokeAsync();
            }
            else
            {
                _saveError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Edit.Fallback"];
            }
        }
        finally { _busy = false; }
    }
}
