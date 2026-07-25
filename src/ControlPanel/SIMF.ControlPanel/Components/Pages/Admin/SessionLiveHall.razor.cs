using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>2026-07-18 (CP page 2e) — the live per-session hall view. The admin
/// picks a session and sees, live, its 4-state seat map (available / unavailable
/// / reserved / confirmed) plus everyone currently inside the hall with their
/// profile + seat. Read-only: it pulls the admin seat-map read and the present
/// list (both API-side gated <c>Attendance.View</c>) and re-pulls both on
/// <see cref="RefreshAsync"/>. No writes.</summary>
public partial class SessionLiveHall
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummary> _sessions = new();
    private AdminSessionSummary? _selected;
    private SessionSeatMap? _map;
    private IReadOnlyList<SessionPresentAttendee> _present = Array.Empty<SessionPresentAttendee>();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;
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
                    ?? L["Admin.SessionLiveHall.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnSessionChangedAsync(AdminSessionSummary? session)
    {
        // Clear any stale toast / prior hall so session A's data never bleeds into B.
        _toast = null;
        _selected = session;
        _map = null;
        _present = Array.Empty<SessionPresentAttendee>();
        if (session is not null)
        {
            await LoadHallAsync();
        }
    }

    private async Task RefreshAsync()
    {
        _toast = null;
        await LoadHallAsync();
    }

    private async Task LoadHallAsync()
    {
        if (_selected is null) return;
        _busy = true;
        try
        {
            var mapEnv = await JS.InvokeAsync<ApiResult<SessionSeatMap>>(
                "simfAccount.getJson",
                $"/account/api/admin/sessions/{_selected.Id}/seat-map");
            var presentEnv = await JS.InvokeAsync<ApiResult<List<SessionPresentAttendee>>>(
                "simfAccount.getJson",
                $"/account/api/admin/sessions/{_selected.Id}/present");

            if (mapEnv is { Success: true, Data: not null })
            {
                _map = mapEnv.Data;
            }
            else
            {
                _toast = new Toast("error",
                    mapEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionLiveHall.LoadFailed"]);
            }

            if (presentEnv is { Success: true, Data: not null })
            {
                _present = presentEnv.Data;
            }
            else
            {
                // /present and /seat-map fail independently. Surface the failure
                // (don't silently show an empty hall) and clear the list so a
                // failed Refresh can't leave a stale, misleading roster on screen.
                _present = Array.Empty<SessionPresentAttendee>();
                _toast = new Toast("error",
                    presentEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionLiveHall.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    // -- Seat-map rendering (read-only 4-state grid) -------------------------

    // D-767 — seats in row i: the per-row SeatCounts entry when the layout is
    // variable (ragged), else the uniform SeatsPerRow. Tolerant of a short/absent
    // SeatCounts so a length-mismatched payload still renders.
    private int SeatsInRow(int rowIndex) => _map is null ? 0
        : (_map.SeatCounts is { Count: > 0 } sc && rowIndex < sc.Count
            ? sc[rowIndex]
            : _map.SeatsPerRow);

    // Fast (row,seat) -> reserved-cell lookup so the grid renders O(1) per seat.
    private SessionSeatCell? FindCell(string rowLabel, int seatNumber) =>
        _map?.ReservedCells.FirstOrDefault(c =>
            string.Equals(c.RowLabel, rowLabel, StringComparison.OrdinalIgnoreCase)
            && c.SeatNumber == seatNumber);

    // The four seat states: available (no cell) · unavailable (admin block) ·
    // reserved (a holder not yet checked in) · confirmed (a holder checked in).
    private static string SeatStateClass(SessionSeatCell? cell)
    {
        if (cell is null) { return "seatmap__seat--available"; }
        if (cell.Kind == SeatReservationKind.AdminReservedRow)
        {
            return "seatmap__seat--unavailable";
        }
        return cell.CheckedIn
            ? "seatmap__seat--confirmed"
            : "seatmap__seat--reserved";
    }

    private string SeatStateLabel(SessionSeatCell? cell)
    {
        if (cell is null) { return L["Admin.SessionLiveHall.Seat.Available"]; }
        if (cell.Kind == SeatReservationKind.AdminReservedRow)
        {
            return L["Admin.SessionLiveHall.Seat.Unavailable"];
        }
        return cell.CheckedIn
            ? L["Admin.SessionLiveHall.Seat.Confirmed"]
            : L["Admin.SessionLiveHall.Seat.Reserved"];
    }

    private string SeatTitle(string rowLabel, int seatNumber, SessionSeatCell? cell) =>
        string.Format(L["Admin.SessionLiveHall.SeatTitle"],
            $"{rowLabel}{seatNumber}", SeatStateLabel(cell));

    // -- Present list --------------------------------------------------------

    private string PresentName(SessionPresentAttendee attendee) =>
        string.IsNullOrWhiteSpace(attendee.Name) ? attendee.NameArabic : attendee.Name;

    private string SeatLabel(SessionPresentAttendee attendee) =>
        attendee.RowLabel is not null && attendee.SeatNumber is not null
            ? $"{attendee.RowLabel}{attendee.SeatNumber}"
            : L["Admin.SessionLiveHall.OpenSeating"];

    private static string EnteredUtc(DateTimeOffset enterUtc) =>
        enterUtc.FormatSaudi("yyyy-MM-dd hh:mm tt");

    private string MethodLabel(AttendanceMethod method) => method switch
    {
        AttendanceMethod.QrScan => L["Admin.SessionLiveHall.Method.QrScan"],
        AttendanceMethod.Geofence => L["Admin.SessionLiveHall.Method.Geofence"],
        _ => method.ToString(),
    };
}
