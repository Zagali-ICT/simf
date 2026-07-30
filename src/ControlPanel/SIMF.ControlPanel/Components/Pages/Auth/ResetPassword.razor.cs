using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class ResetPassword
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly ResetModel _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _loading;
    private bool _done;
    private string? _error;

    protected override void OnInitialized()
    {
        _model.Email = Session.PendingEmail ?? string.Empty;
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += ClearFieldError;
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

            if (!SimfAuthSession.LooksLikeEmail(_model.Email))
            {
                _messages.Add(_editContext.Field(nameof(ResetModel.Email)), L["Auth.SignIn.EmailInvalid"]);
            }

            if (!SimfAuthSession.IsSixDigitCode(_model.Code))
            {
                _messages.Add(_editContext.Field(nameof(ResetModel.Code)), L["Auth.Totp.CodeRequired"]);
            }

            var passwordError = ValidatePassword(_model.NewPassword);
            if (passwordError is not null)
            {
                _messages.Add(_editContext.Field(nameof(ResetModel.NewPassword)), passwordError);
            }

            if (_model.ConfirmPassword != _model.NewPassword)
            {
                _messages.Add(_editContext.Field(nameof(ResetModel.ConfirmPassword)),
                    L["Auth.Reset.PasswordsMismatch"]);
            }

            _editContext.NotifyValidationStateChanged();
            if (_editContext.GetValidationMessages().Any())
            {
                return;
            }

            var result = await Api.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = _model.Email,
                Code = _model.Code,
                NewPassword = _model.NewPassword,
                ConfirmPassword = _model.ConfirmPassword,
            });

            if (!result.Success)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Reset.Fallback"];
                return;
            }

            Session.Clear();
            _done = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToSignIn() => Nav.NavigateTo("/login");

    // Mirrors the API password policy (SIMF-API-001 section 12.5).
    private string? ValidatePassword(string password) =>
        password.Length < 8 ? L["Auth.Reset.PasswordTooShort"].Value
        : password.Length > 128 ? L["Auth.Reset.PasswordTooLong"].Value
        : !password.Any(char.IsDigit) ? L["Auth.Reset.PasswordNoDigit"].Value
        : !password.Any(char.IsLetter) ? L["Auth.Reset.PasswordNoLetter"].Value
        : null;

    private sealed class ResetModel
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
