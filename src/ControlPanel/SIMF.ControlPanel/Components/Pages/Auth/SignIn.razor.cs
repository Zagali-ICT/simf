using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class SignIn
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private SignInTicketStore Tickets { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly SignInModel _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _loading;
    private string? _error;
    private string? _info;

    // Forced-change popup state.
    private bool _mustChangePassword;
    private string? _passwordChangeToken;
    private bool _changeBusy;
    private string? _changeError;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += ClearFieldError;
    }

    // A field the user is correcting clears its own error straight away.
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
        _info = null;
        try
        {
            _messages.Clear();

            if (string.IsNullOrWhiteSpace(_model.Email))
            {
                _messages.Add(_editContext.Field(nameof(SignInModel.Email)), L["Auth.SignIn.EmailRequired"]);
            }
            else if (!SimfAuthSession.LooksLikeEmail(_model.Email))
            {
                _messages.Add(_editContext.Field(nameof(SignInModel.Email)), L["Auth.SignIn.EmailInvalid"]);
            }

            if (string.IsNullOrEmpty(_model.Password))
            {
                _messages.Add(_editContext.Field(nameof(SignInModel.Password)), L["Auth.SignIn.PasswordRequired"]);
            }

            _editContext.NotifyValidationStateChanged();
            if (_editContext.GetValidationMessages().Any())
            {
                return;
            }

            var result = await Api.SignInAsync(
                new SignInRequest
                {
                    Email = _model.Email,
                    Password = _model.Password,
                    Audience = SignInAudience.Cp,   // CP-only surface
                });

            if (!result.Success || result.Data is null)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.SignIn.Fallback"];
                return;
            }

            // A forced-change Control Panel credential comes back with a
            // single-use ticket instead of a session — open the change-password
            // popup instead of routing to the second factor. The ticket
            // authorises the change; the user signs in normally afterwards.
            if (result.Data.PasswordChangeToken is not null)
            {
                _passwordChangeToken = result.Data.PasswordChangeToken;
                _changeError = null;
                _mustChangePassword = true;
                return;
            }

            // The account proved its password but carries no
            // authenticator secret, so the API withheld the session and issued a
            // mandatory-enrolment ticket instead. Route to the enrolment page —
            // it pairs an authenticator and the completion issues the session.
            if (result.Data.TwoFactorEnrolmentToken is not null)
            {
                Session.PendingEmail = _model.Email;
                Session.PendingEnrolmentToken = result.Data.TwoFactorEnrolmentToken;
                Nav.NavigateTo("/login/enrol-2fa");
                return;
            }

            // Skip the second factor when the account has TwoFactorEnabled=false.
            // The API answers with tokens immediately in that
            // case; no MfaToken is returned. Stash the optional
            // AccountStateInfo too so the cookie carries the rejection
            // reason for the state-banner page.
            if (result.Data.MfaToken is null && result.Data.Tokens is not null)
            {
                var reference = Tickets.Stash(result.Data.Tokens, result.Data.AccountState);
                Session.Clear();
                Nav.NavigateTo($"/auth/complete?reference={reference}", forceLoad: true);
                return;
            }

            // The usual Control Panel path — complete with the authenticator code.
            Session.PendingEmail = _model.Email;
            Session.PendingMfaToken = result.Data.MfaToken;
            Nav.NavigateTo("/login/totp");
        }
        finally
        {
            _loading = false;
        }
    }

    // Complete the forced password change against the ticket. On success
    // no session is minted — the operator signs in again with the new password,
    // so we close the popup, clear the password field and show a success note.
    private async Task CompletePasswordChangeAsync(ChangePasswordCard.Model model)
    {
        if (_changeBusy)
        {
            return;
        }

        _changeBusy = true;
        _changeError = null;
        try
        {
            var result = await Api.CompletePasswordChangeAsync(new CompletePasswordChangeRequest
            {
                PasswordChangeToken = _passwordChangeToken ?? string.Empty,
                NewPassword = model.NewPassword,
                ConfirmPassword = model.ConfirmPassword,
            });

            if (!result.Success)
            {
                _changeError = result.Error?.DetailedMessageForCurrentCulture()
                    ?? L["Auth.ChangePassword.Fallback"];
                return;
            }

            _mustChangePassword = false;
            _passwordChangeToken = null;
            _model.Password = string.Empty;
            _info = L["Auth.ChangePassword.Success"];
        }
        finally
        {
            _changeBusy = false;
        }
    }

    private void CloseChangePassword()
    {
        _mustChangePassword = false;
        _passwordChangeToken = null;
        _changeError = null;
    }

    private sealed class SignInModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
