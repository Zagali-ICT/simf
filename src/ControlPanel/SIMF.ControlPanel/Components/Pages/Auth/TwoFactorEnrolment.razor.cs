using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class TwoFactorEnrolment
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private SignInTicketStore Tickets { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private readonly CodeModel _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;

    private bool _loadingSetup = true;
    private bool _submitting;
    private string? _error;
    private TotpSetupResponse? _setup;

    // Stage 2 state — set once the enrolment completes. The tokens are held
    // until the user acknowledges the recovery codes, which are shown exactly
    // once (D-040); the hand-off to the cookie endpoint happens on Continue.
    private IReadOnlyList<string>? _recoveryCodes;
    private AuthTokens? _tokens;

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += ClearFieldError;

        // Enrolment is only reachable after the password step.
        if (string.IsNullOrEmpty(Session.PendingEnrolmentToken))
        {
            Nav.NavigateTo("/login");
            return;
        }

        var result = await Api.StartTwoFactorEnrolmentAsync(
            new StartTwoFactorEnrolmentRequest
            {
                EnrolmentToken = Session.PendingEnrolmentToken!,
            });

        _loadingSetup = false;
        if (!result.Success || result.Data is null)
        {
            _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Enrol2fa.Fallback"];
            return;
        }
        _setup = result.Data;
    }

    // A field the user is correcting clears its own error straight away.
    private void ClearFieldError(object? sender, FieldChangedEventArgs e)
    {
        _messages.Clear(e.FieldIdentifier);
        _editContext.NotifyValidationStateChanged();
    }

    private async Task HandleSubmitAsync()
    {
        if (_submitting)
        {
            return;
        }

        _submitting = true;
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

            var result = await Api.CompleteTwoFactorEnrolmentAsync(
                new CompleteTwoFactorEnrolmentRequest
                {
                    EnrolmentToken = Session.PendingEnrolmentToken ?? string.Empty,
                    Code = _model.Code,
                });

            if (!result.Success || result.Data is null)
            {
                _error = result.Error?.MessageForCurrentCulture() ?? L["Auth.Enrol2fa.Fallback"];
                return;
            }

            _tokens = result.Data.Tokens;
            _recoveryCodes = result.Data.RecoveryCodes;
        }
        finally
        {
            _submitting = false;
        }
    }

    // Hand the completed sign-in to the cookie-issuing endpoint — a cookie can
    // only be written in an HTTP request context. Same hand-off the TOTP step
    // uses, so the enrolled admin lands in the shell exactly as usual.
    private void ContinueToControlPanel()
    {
        if (_tokens is null)
        {
            Nav.NavigateTo("/login");
            return;
        }

        var reference = Tickets.Stash(_tokens);
        Session.Clear();
        Nav.NavigateTo($"/auth/complete?reference={reference}", forceLoad: true);
    }

    private sealed class CodeModel
    {
        public string Code { get; set; } = string.Empty;
    }
}
