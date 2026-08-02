using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SpeakersViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    private static string? CountryLabel(AdminSpeakerDetail row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.CountryNameAr ?? row.CountryNameEn
            : row.CountryNameEn ?? row.CountryNameAr;

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/speakers/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Speakers.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Speakers.Fallback"];
        }
        finally { _busy = false; }
    }
}
