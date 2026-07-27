using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

// #6/#17 (owner 2026-07-20) — read-only booking monitor. Bookings auto-confirm
// (no approval step) and no-show seats are released automatically by
// ReservationNoShowReleaseWorker, so this page only lists + exports the active
// (confirmed, still-held) reservations — the approve / reject / bulk-approve
// actions were removed.
public partial class BookingsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<ActiveBookingRow> _page = new();
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<ActiveBookingRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/bookings/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Bookings.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — bookings are created
    // by visitors in the app and auto-confirm (#6), so there is no import path.
    // The booking's key is ReservationId.
    private async Task OnExportAsync(IReadOnlyList<ActiveBookingRow> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/bookings/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.ReservationId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }
}
