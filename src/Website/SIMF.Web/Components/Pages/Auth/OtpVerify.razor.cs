using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Web.Components.Pages.Auth;

// Website — the second factor: a 6-digit code emailed to the visitor. Reached
// only from the sign-in step when the account has TwoFactorEnabled=true. Markup
// lives in OtpVerify.razor.
public partial class OtpVerify
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

        // The verification step is only reachable after the password step.
        if (string.IsNullOrEmpty(Session.PendingOtpToken))
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
                _messages.Add(_editContext.Field(nameof(CodeModel.Code)), L["Auth.Verify.CodeRequired"]);
            }

            _editContext.NotifyValidationStateChanged();
            if (_editContext.GetValidationMessages().Any())
            {
                return;
            }

            var result = await Api.VerifyOtpAsync(
                new VerifyOtpRequest { OtpToken = Session.PendingOtpToken!, Code = _model.Code });

            if (!result.Success || result.Data is null)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Verify.Fallback"];
                return;
            }

            Session.Complete(result.Data);
            // Hand off to the cookie-writing /auth/complete endpoint via a
            // one-time ticket so the visitor lands on the profile page with
            // a real session cookie (D-046 c).
            var reference = Tickets.Stash(result.Data);
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
