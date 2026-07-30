using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ResetTwoFactor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _busy;
    private bool _success;
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
        _success = false;
        _editContext.NotifyValidationStateChanged();
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy)
        {
            return;
        }

        _messages.Clear();
        _success = false;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Email))
        {
            _messages.Add(_editContext.Field(nameof(Model.Email)),
                L["Admin.ResetTwoFactor.EmailRequired"]);
        }
        else if (!_model.Email.Contains('@', StringComparison.Ordinal))
        {
            _messages.Add(_editContext.Field(nameof(Model.Email)),
                L["Admin.ResetTwoFactor.EmailInvalid"]);
        }

        if (_model.Reason.Length < 10 || _model.Reason.Length > 500)
        {
            _messages.Add(_editContext.Field(nameof(Model.Reason)),
                L["Admin.ResetTwoFactor.ReasonRequired"]);
        }

        _editContext.NotifyValidationStateChanged();
        if (_editContext.GetValidationMessages().Any())
        {
            return;
        }

        // Last-line confirmation — the reset signs the target out of every
        // session and wipes their codes. No way to undo.
        var confirmed = await JS.InvokeAsync<bool>(
            "confirm", (string)L["Admin.ResetTwoFactor.Confirm"]);
        if (!confirmed)
        {
            return;
        }

        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                "/account/api/admin/reset-2fa",
                new AdminResetTwoFactorRequest
                {
                    Email = _model.Email,
                    Reason = _model.Reason,
                });

            if (envelope is { Success: true })
            {
                _success = true;
                _model.Email = string.Empty;
                _model.Reason = string.Empty;
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ResetTwoFactor.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Email { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
