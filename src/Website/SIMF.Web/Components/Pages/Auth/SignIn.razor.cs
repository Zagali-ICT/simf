using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;

namespace SIMF.Web.Components.Pages.Auth;

// Website — sign-in (the password step). A visitor account completes with a code
// emailed to the account — unless TwoFactorEnabled is false, in which case the
// API returns the tokens directly. Markup lives in SignIn.razor.
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
                    Audience = SignInAudience.Web,  // P2 — visitor surface (also the default)
                });

            if (!result.Success || result.Data is null)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.SignIn.Fallback"];
                return;
            }

            // Skip the second factor when the account has TwoFactorEnabled=false
            // (myComment #34). The API answers with tokens immediately. Hand
            // off to the cookie-writing /auth/complete endpoint via a
            // one-time ticket (D-046 c). P11 — D-052: stash the optional
            // AccountStateInfo so the cookie carries the rejection
            // reason for the state-banner page.
            if (result.Data.OtpToken is null && result.Data.Tokens is not null)
            {
                var reference = Tickets.Stash(result.Data.Tokens, result.Data.AccountState);
                Nav.NavigateTo($"/auth/complete?reference={reference}", forceLoad: true);
                return;
            }

            // The usual visitor path — a code is emailed to the account.
            Session.PendingEmail = _model.Email;
            Session.PendingOtpToken = result.Data.OtpToken;
            Nav.NavigateTo("/login/verify");
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed class SignInModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
