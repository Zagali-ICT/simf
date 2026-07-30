using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class PrintBag
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string _qrId = string.Empty;
    private bool _busy;
    private string? _error;
    private AdminWalkInRegistrationResponse? _result;
    private string _qrSvg = string.Empty;

    private string TypeLabel
    {
        get
        {
            if (_result is null) { return string.Empty; }
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                ? _result.ProfileTypeNameArabic
                : _result.ProfileTypeName;
        }
    }

    private string EmailLabel
    {
        get
        {
            if (_result is null) { return string.Empty; }
            if (_result.Email.EndsWith("@simf.local", StringComparison.OrdinalIgnoreCase))
            {
                return L["Admin.WalkIn.Success.NoEmail"];
            }
            return _result.Email;
        }
    }

    private async Task OnSearchAsync()
    {
        if (_busy) return;
        var query = (_qrId ?? string.Empty).Trim();
        _error = null;
        _result = null;
        _qrSvg = string.Empty;
        if (query.Length == 0)
        {
            _error = L["Admin.PrintBag.Error.Required"];
            return;
        }
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminWalkInRegistrationResponse>>(
                "simfAccount.getJson",
                $"/account/api/admin/qr-lookup/{Uri.EscapeDataString(query)}");
            if (envelope is { Success: true, Data: not null })
            {
                _result = envelope.Data;
                _qrSvg = BadgeQrCode.ToSvg(_result.QrId);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.PrintBag.Error.NotFound"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.PrintBag.Error.NotFound"];
        }
        finally { _busy = false; }
    }

    private async Task OnPrintAsync() =>
        await JS.InvokeVoidAsync("window.print");

    private async Task OnResetAsync()
    {
        _result = null;
        _qrSvg = string.Empty;
        _error = null;
        _qrId = string.Empty;
        // The Search field auto-focuses on next render so the desk can
        // immediately scan the next QR.
        await Task.Yield();
        StateHasChanged();
    }

}
