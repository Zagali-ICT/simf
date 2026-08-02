using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class ForgotPassword
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly EmailModel _model = new();
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
                _messages.Add(_editContext.Field(nameof(EmailModel.Email)), L["Auth.SignIn.EmailRequired"]);
            }
            else if (!SimfAuthSession.LooksLikeEmail(_model.Email))
            {
                _messages.Add(_editContext.Field(nameof(EmailModel.Email)), L["Auth.SignIn.EmailInvalid"]);
            }

            _editContext.NotifyValidationStateChanged();
            if (_editContext.GetValidationMessages().Any())
            {
                return;
            }

            var result = await Api.ForgotPasswordAsync(
                new ForgotPasswordRequest { Email = _model.Email });

            if (!result.Success)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Forgot.Fallback"];
                return;
            }

            // The API answers the same whether or not the account exists; the flow
            // always proceeds to the reset step.
            Session.PendingEmail = _model.Email;
            Nav.NavigateTo("/reset-password");
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed class EmailModel
    {
        public string Email { get; set; } = string.Empty;
    }
}
