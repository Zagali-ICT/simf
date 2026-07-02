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

public partial class CreateAdminForm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _busy;
    private string? _success;
    private string? _error;

    // Issue-1 — the assignable CP roles (fetched) and the admin's selection.
    private readonly List<string> _roleNames = new();
    private readonly HashSet<string> _selectedRoles = new(StringComparer.Ordinal);
    private bool _rolesLoading = true;

    /// <summary>Raised after a successful create. Receives the API response.</summary>
    [Parameter] public EventCallback<AdminCreateUserResponse> OnSuccess { get; set; }

    /// <summary>Raised when the user clicks Cancel. If unset, no Cancel button renders.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>The cancel button label. Defaults to the standard back-to-list copy.</summary>
    [Parameter] public string? CancelLabel { get; set; }

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += ClearFieldError;
        CancelLabel ??= L["Admin.CreateUser.BackToList"];
    }

    protected override async Task OnInitializedAsync()
    {
        // Issue-1 — load the assignable roles for the multi-select. If the
        // caller lacks Roles.View the list comes back empty; the form still
        // creates the user (with no roles) so it degrades gracefully.
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminRoleSummary>>>(
                "simfAccount.postJson", "/account/api/admin/roles/list",
                new GridQuery { Top = 200, Sort = "name" });
            if (envelope is { Success: true, Data: not null })
            {
                _roleNames.AddRange(envelope.Data.Items.Select(role => role.Name));
            }
        }
        catch (JSException)
        {
            // Best-effort — leave the role list empty on a transport error.
        }
        finally { _rolesLoading = false; }
    }

    private void ToggleRole(string roleName, bool on)
    {
        if (on) { _selectedRoles.Add(roleName); }
        else { _selectedRoles.Remove(roleName); }
    }

    private void ClearFieldError(object? sender, FieldChangedEventArgs e)
    {
        _messages.Clear(e.FieldIdentifier);
        _success = null;
        _editContext.NotifyValidationStateChanged();
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;

        _messages.Clear();
        _success = null;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Email) || !_model.Email.Contains('@', StringComparison.Ordinal))
        {
            _messages.Add(_editContext.Field(nameof(Model.Email)),
                L["Admin.CreateUser.EmailInvalid"]);
        }
        if (_model.DisplayName.Length is < 2 or > 128)
        {
            _messages.Add(_editContext.Field(nameof(Model.DisplayName)),
                L["Admin.CreateUser.DisplayNameRequired"]);
        }

        _editContext.NotifyValidationStateChanged();
        if (_editContext.GetValidationMessages().Any()) return;

        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminCreateUserResponse>>(
                "simfAccount.postJson", "/account/api/admin/admins",
                new AdminCreateAdminRequest
                {
                    Email = _model.Email,
                    DisplayName = _model.DisplayName,
                    Roles = _selectedRoles.ToList(),
                });
            if (envelope is { Success: true, Data: not null })
            {
                _success = string.Format(L["Admin.CreateUser.Success"], envelope.Data.Email);
                _model.Email = string.Empty;
                _model.DisplayName = string.Empty;
                _selectedRoles.Clear();
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.CreateUser.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
