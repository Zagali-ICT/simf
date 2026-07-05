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
using SIMF.Contracts.Faq;
using SIMF.Contracts.Exhibition;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VenueMapViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    private List<AdminHallSummary> _halls = new();
    private List<AdminBoothSummary> _booths = new();

    private string HallName =>
        Initial?.HallId is { } id
            ? _halls.FirstOrDefault(h => h.Id == id)?.Name ?? id.ToString()
            : "—";

    private string BoothName =>
        Initial?.BoothId is { } id
            ? _booths.FirstOrDefault(b => b.Id == id)?.Name ?? id.ToString()
            : "—";

    protected override async Task OnInitializedAsync()
    {
        // Only fetch the lookups when there is a link to resolve — a node with no
        // Hall/Booth shows the em-dash without a round-trip.
        if (Initial?.HallId is not null)
        {
            var halls = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
                "simfAccount.postJson", "/account/api/admin/halls/list", new GridQuery { Top = 500 });
            if (halls is { Success: true, Data: not null }) _halls = halls.Data.Items.ToList();
        }
        if (Initial?.BoothId is not null)
        {
            var booths = await JS.InvokeAsync<ApiResult<GridPage<AdminBoothSummary>>>(
                "simfAccount.postJson", "/account/api/admin/booths/list", new GridQuery { Top = 500 });
            if (booths is { Success: true, Data: not null }) _booths = booths.Data.Items.ToList();
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
                "simfAccount.deleteJson", $"/account/api/admin/venue-map/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                // Close the confirm first so the error lands on the visible form
                // body, not behind the (still-open) confirm overlay.
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.VenueMap.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.VenueMap.Fallback"];
        }
        finally { _busy = false; }
    }
}
