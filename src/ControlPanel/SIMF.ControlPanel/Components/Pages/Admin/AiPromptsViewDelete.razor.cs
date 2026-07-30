using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiPromptsViewDelete
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
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/ai/prompts/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                // Close the confirm first so the error lands on the visible
                // form body, not behind the (still-open) confirm overlay.
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiPrompts.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.AiPrompts.LoadFailed"];
        }
        finally { _busy = false; }
    }
}
