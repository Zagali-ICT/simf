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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionSeatPlan
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummary> _sessions = new();
    private Guid? _selectedSessionId;
    private List<SessionSeatCell> _reservations = new();
    // P1.4 (D-215) — the hall layout for the selected session's hall, used to
    // paint the full grid (free + reserved). Null when the hall has no layout.
    private HallSeatLayoutSnapshot? _layout;
    private string _reserveRowLabel = string.Empty;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // P1.4 — fast (row,seat) -> reservation lookup so the grid renders O(1) per seat.
    private SessionSeatCell? FindReservation(string rowLabel, int seatNumber) =>
        _reservations.FirstOrDefault(r =>
            string.Equals(r.RowLabel, rowLabel, StringComparison.OrdinalIgnoreCase)
            && r.SeatNumber == seatNumber);

    private static string SeatStateClass(SessionSeatCell? cell) => cell?.Kind switch
    {
        SeatReservationKind.UserBooking => "seatgrid__seat--user seatgrid__seat--reserved",
        SeatReservationKind.AdminReservedRow => "seatgrid__seat--admin seatgrid__seat--reserved",
        SeatReservationKind.RandomAssignment => "seatgrid__seat--random seatgrid__seat--reserved",
        _ => string.Empty,
    };

    private string SeatTitle(string rowLabel, int seatNumber, SessionSeatCell? cell)
    {
        var coords = $"{rowLabel}{seatNumber}";
        return cell is null
            ? string.Format(L["Admin.SessionSeatPlans.Seat.FreeTitle"], coords)
            : string.Format(L["Admin.SessionSeatPlans.Seat.ReservedTitle"], coords, cell.Kind);
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.postJson", "/account/api/admin/sessions/list",
                new GridQuery { Top = 200 });
            if (env is { Success: true, Data: not null })
            {
                _sessions = env.Data.Items.Where(s => s.IsActive)
                    .OrderBy(s => s.Code).ToList();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSeatPlans.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnSessionChangedAsync(ChangeEventArgs e)
    {
        // Clear any stale toast so a "Released" / "ReserveRow.Done"
        // message from session A doesn't follow the admin to session B.
        _toast = null;
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            _selectedSessionId = id;
            await LoadReservationsAsync();
        }
        else
        {
            _selectedSessionId = null;
            _reservations.Clear();
            _layout = null;
        }
    }

    private async Task LoadReservationsAsync()
    {
        if (_selectedSessionId is null) return;
        _loading = true;
        try
        {
            await LoadLayoutForSelectedSessionAsync();
            var env = await JS.InvokeAsync<ApiResult<GridPage<SessionSeatCell>>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/seats/list",
                new GridQuery { Top = 500 });
            if (env is { Success: true, Data: not null })
            {
                _reservations = env.Data.Items.ToList();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSeatPlans.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // P1.4 (D-215) — fetch the hall layout for the selected session's hall so
    // the grid can show free seats too. A missing layout (404) is not an error;
    // the page falls back to the reservation list.
    private async Task LoadLayoutForSelectedSessionAsync()
    {
        _layout = null;
        var session = _sessions.FirstOrDefault(s => s.Id == _selectedSessionId);
        if (session is null) return;
        var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
            "simfAccount.getJson",
            $"/account/api/admin/halls/{session.HallId}/seat-layout");
        if (env is { Success: true, Data: not null })
        {
            _layout = env.Data;
        }
    }

    private void OnReserveRowChanged(ChangeEventArgs e) =>
        _reserveRowLabel = (e.Value?.ToString() ?? string.Empty).Trim();

    private async Task ReserveRowAsync()
    {
        if (_selectedSessionId is null || _busy
            || string.IsNullOrWhiteSpace(_reserveRowLabel))
        {
            return;
        }
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/seats/reserve-row",
                new AdminReserveRowRequest { RowLabel = _reserveRowLabel });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SessionSeatPlans.ReserveRow.Done"]);
                _reserveRowLabel = string.Empty;
                await LoadReservationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSeatPlans.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    // 2026-07-18 — reserve a single free seat for a VIP (an admin block on that
    // one seat). Tapping a free seat in the grid calls this.
    private async Task ReserveSeatAsync(string rowLabel, int seatNumber)
    {
        if (_selectedSessionId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/seats/reserve-seat",
                new AdminReserveSeatRequest { RowLabel = rowLabel, SeatNumber = seatNumber });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SessionSeatPlans.ReserveSeat.Done"]);
                await LoadReservationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSeatPlans.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task ReleaseAsync(SessionSeatCell row)
    {
        if (_selectedSessionId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/seats/{row.ReservationId}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SessionSeatPlans.Released"]);
                await LoadReservationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSeatPlans.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
