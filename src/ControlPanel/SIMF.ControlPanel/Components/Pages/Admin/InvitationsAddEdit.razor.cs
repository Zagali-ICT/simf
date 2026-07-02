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

public partial class InvitationsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private static readonly InvitationState[] _invitationStates =
        Enum.GetValues<InvitationState>();

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.State = Initial.State;
            _model.Notes = Initial.Notes ?? string.Empty;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (!IsEdit && !Guid.TryParse(_model.RecipientId.Trim(), out _))
        {
            _error = L["Admin.Invitations.RecipientRequired"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminInvitationDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminInvitationDetail>>(
                    "simfAccount.postJson", "/account/api/admin/invitations",
                    new AdminCreateInvitationRequest
                    {
                        SentToUserProfileId = Guid.Parse(_model.RecipientId.Trim()),
                        Notes = NullIfBlank(_model.Notes),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminInvitationDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/invitations/{Initial!.Id}",
                    new AdminUpdateInvitationRequest
                    {
                        State = _model.State,
                        Notes = NullIfBlank(_model.Notes),
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Invitations.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Invitations.Fallback"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string RecipientId { get; set; } = string.Empty;
        public InvitationState State { get; set; } = InvitationState.Pending;
        public string Notes { get; set; } = string.Empty;
    }
}
