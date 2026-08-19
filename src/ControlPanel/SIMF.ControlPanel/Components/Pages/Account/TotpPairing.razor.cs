using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Account;

public partial class TotpPairing
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading;
    private bool _verifying;
    private bool _noSecret;
    private TotpSetupResponse? _pairing;
    private string? _loadError;
    private string? _verifyOk;
    private string? _verifyError;
    private readonly CodeFormModel _revealModel = new();
    private readonly CodeFormModel _codeModel = new();

    /// <summary>Reveal the pairing, in exchange for a code from the authenticator
    /// the caller already holds.
    ///
    /// <para>This used to run on page load with no challenge at all, which handed
    /// the account's TOTP secret in plaintext to anything holding a bearer token.
    /// It is a deliberate action now, and the API refuses it without a valid
    /// code.</para></summary>
    private async Task RevealAsync()
    {
        if (_loading) { return; }
        _loading = true;
        _loadError = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpSetupResponse>>(
                "simfAccount.postJson", "/account/api/totp/pairing",
                new TotpConfirmRequest { Code = _revealModel.Code });
            if (envelope is { Success: true, Data: not null })
            {
                _pairing = envelope.Data;
                _revealModel.Code = string.Empty;
            }
            else if (envelope?.Error is null || envelope.Error.Code == ErrorCodes.NotFound)
            {
                // A 404 arrives as a failed envelope with no Data and no specific
                // code: the account has no active secret, so this page has nothing
                // to show and the user belongs on Profile to enrol.
                _noSecret = true;
            }
            else
            {
                _loadError = envelope.Error.MessageForCurrentCulture()
                    ?? L["TotpPairing.LoadError"];
            }
        }
        catch
        {
            _loadError = L["TotpPairing.LoadError"];
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task VerifyAsync()
    {
        _verifyOk = null;
        _verifyError = null;
        _verifying = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpPairingVerifyResponse>>(
                "simfAccount.postJson", "/account/api/totp/pairing/verify",
                new TotpConfirmRequest { Code = _codeModel.Code });
            if (envelope is { Success: true, Data: { Valid: true } })
            {
                _verifyOk = L["TotpPairing.VerifySuccess"];
                _codeModel.Code = string.Empty;
            }
            else if (envelope?.Error is not null)
            {
                _verifyError = envelope.Error.MessageForCurrentCulture()
                    ?? L["TotpPairing.VerifyError"];
            }
            else
            {
                _verifyError = L["TotpPairing.VerifyError"];
            }
        }
        catch
        {
            _verifyError = L["TotpPairing.VerifyError"];
        }
        finally
        {
            _verifying = false;
        }
    }

    private sealed class CodeFormModel
    {
        public string Code { get; set; } = string.Empty;
    }
}
