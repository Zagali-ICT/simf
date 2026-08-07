using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class GatesViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    /// <summary>BUG-018 (18-6) — the detail view listed only the operator COUNT, so
    /// an assignment could not be audited from the CP. The gate's assignments
    /// (operator name + email) are loaded from the existing Gates.Manage-gated
    /// assignments endpoint.</summary>
    private IReadOnlyList<AdminGateAssignmentRow> _operators =
        Array.Empty<AdminGateAssignmentRow>();
    private bool _operatorsFailed;

    protected override async Task OnInitializedAsync()
    {
        if (Initial is null) return;

        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminGateAssignmentRow>>>(
            "simfAccount.getJson", $"/account/api/admin/gates/{Initial.Id}/assignments");
        if (envelope is { Success: true, Data: { } rows })
        {
            _operators = rows;
        }
        else
        {
            _operatorsFailed = true;
        }
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/gates/{Initial.Id}");
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
                    ?? L["Admin.Gates.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Gates.Fallback"];
        }
        finally { _busy = false; }
    }
}
