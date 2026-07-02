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

namespace SIMF.ControlPanel.Components.Pages.Account;

public partial class TotpPairing
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private bool _verifying;
    private TotpSetupResponse? _pairing;
    private string? _loadError;
    private string? _verifyOk;
    private string? _verifyError;
    private readonly CodeFormModel _codeModel = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpSetupResponse>>(
                "simfAccount.getJson", "/account/api/totp/pairing");
            if (envelope is { Success: true, Data: not null })
            {
                _pairing = envelope.Data;
            }
            else if (envelope?.Error is null || envelope.Error.Code == ErrorCodes.NotFound)
            {
                // 404 from the API arrives as a failed envelope with no Data
                // and no specific error code; treat as "no secret" and route
                // the user to the Profile page to enrol.
                _pairing = null;
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
