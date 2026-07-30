using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ExhibitorsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/exhibitors/{Initial.Id}");
            if (env is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                _confirming = false;
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Exhibitors.LoadFailed"];
        }
        finally { _busy = false; }
    }
}
