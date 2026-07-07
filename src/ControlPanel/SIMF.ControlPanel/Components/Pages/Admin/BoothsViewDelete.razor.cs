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
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BoothsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    // The detail carries only the exhibitor + hall ids; load the active lookups
    // so the read-only view can show their names (mirrors the list grid columns).
    private List<AdminExhibitorSummary> _exhibitors = new();
    private List<AdminHallSummary> _halls = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadExhibitorsAsync();
        await LoadHallsAsync();
    }

    private async Task LoadExhibitorsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminExhibitorSummary>>>(
            "simfAccount.postJson", "/account/api/admin/exhibitors/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _exhibitors = env.Data.Items.ToList();
        }
    }

    private async Task LoadHallsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _halls = env.Data.Items.ToList();
        }
    }

    private string ExhibitorName(Guid? exhibitorId)
    {
        if (exhibitorId is null) return "—";
        var exhibitor = _exhibitors.FirstOrDefault(c => c.Id == exhibitorId.Value);
        return exhibitor is null ? "—" : exhibitor.NameEn;
    }

    private string HallName(Guid? hallId)
    {
        if (hallId is null) return "—";
        var hall = _halls.FirstOrDefault(h => h.Id == hallId.Value);
        return hall is null ? "—" : hall.Name;
    }

    // D-673 — the booth logo is the linked exhibitor's Contact CompanyLogo asset
    // (the same asset the app renders on the booth). Empty string when the booth
    // has no linked exhibitor / Contact → SimfImageThumb shows its placeholder.
    private static string LogoSrc(Guid? exhibitorContactId) =>
        exhibitorContactId is null
            ? string.Empty
            : $"/account/api/admin/assets/CompanyLogo/{exhibitorContactId}/image";

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/booths/{Initial.Id}");
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
                    ?? L["Admin.Booths.Fallback"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Booths.Fallback"];
        }
        finally { _busy = false; }
    }
}
