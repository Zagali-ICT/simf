using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class TotpVerify
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private SignInTicketStore Tickets { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly CodeModel _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _loading;
    private string? _error;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += ClearFieldError;

        // The TOTP step is only reachable after the password step.
        if (string.IsNullOrEmpty(Session.PendingMfaToken))
        {
            Nav.NavigateTo("/login");
        }
    }

    private void ClearFieldError(object? sender, FieldChangedEventArgs e)
    {
        _messages.Clear(e.FieldIdentifier);
        _editContext.NotifyValidationStateChanged();
    }

    private async Task HandleSubmitAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _error = null;
        try
        {
            _messages.Clear();

            if (!SimfAuthSession.IsSixDigitCode(_model.Code))
            {
                _messages.Add(_editContext.Field(nameof(CodeModel.Code)), L["Auth.Totp.CodeRequired"]);
            }

            _editContext.NotifyValidationStateChanged();
            if (_editContext.GetValidationMessages().Any())
            {
                return;
            }

            var result = await Api.VerifyTotpAsync(
                new VerifyTotpRequest { MfaToken = Session.PendingMfaToken!, Code = _model.Code });

            if (!result.Success || result.Data is null)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Totp.Fallback"];
                return;
            }

            // Hand the completed sign-in to the cookie-issuing endpoint — a
            // cookie can only be written in an HTTP request context.
            var reference = Tickets.Stash(result.Data);
            Session.Clear();
            Nav.NavigateTo($"/auth/complete?reference={reference}", forceLoad: true);
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed class CodeModel
    {
        public string Code { get; set; } = string.Empty;
    }
}
