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
/// <see cref="RefreshAsync"/>. No writes.
/// <para>QA B17 — while a session is selected the two reads also re-run on a
/// <see cref="PeriodicTimer"/> (<see cref="RefreshInterval"/>), mirroring the CP's
/// existing auto-refresh pattern in <c>ServicesMonitor</c>: a door scan used to
/// stay invisible until an admin happened to click Refresh, which makes a "live"
/// monitor misleading during an event. The timer is started on selection, stopped
/// and disposed when the selection clears or the component is disposed, so a
/// Blazor Server circuit never leaks one.</para></summary>
public partial class SessionLiveHall : IDisposable
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    /// <summary>QA B17 — how often the selected session's seat map + present list
    /// are re-pulled while the monitor is open. Matches the CP's other live
    /// monitor (<c>ServicesMonitor</c>) so the two behave the same.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    /// <summary>How many roster rows one pull asks for. The server caps the page
    /// at this figure too, so it is the ceiling either way; <see cref="_presentTotal"/>
    /// is rendered beside the table so a hall holding more than this reads as a
    /// truncated view rather than as the whole room.</summary>
    private const int PresentPageSize = 200;

    /// <summary>The picker's rows come from the ATTENDANCE session list, not the
    /// canonical Sessions list.
    ///
    /// <para>This page is gated <c>Attendance.View</c>, whose baseline role is
    /// SecurityTeam, and the canonical <c>/admin/sessions/list</c> is gated
    /// <c>Sessions.View</c>, which SecurityTeam does not hold. So the page opened
    /// for the exact role it was built for and its first fetch 403'd: an empty
    /// picker, a toast, and nothing an operator could do about it. The attendance
    /// list is gated on this page's own permission and returns every active
    /// session (the counts are a left join, so a hall with no arrivals yet is
    /// still selectable), which is what the picker needs.</para></summary>
    private List<SessionAttendanceRow> _sessions = new();
    private SessionAttendanceRow? _selected;
    private SessionSeatMap? _map;
    private IReadOnlyList<SessionPresentAttendee> _present = Array.Empty<SessionPresentAttendee>();
    private int _presentTotal;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // QA B17 — the auto-refresh loop's timer + cancellation, both null while no
    // session is selected.
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    // QA B17 — one in-flight guard shared by the tick and the Refresh button so a
    // slow pull can never overlap itself.
    private bool _inFlight;

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
            var env = await JS.InvokeAsync<ApiResult<GridPage<SessionAttendanceRow>>>(
                "simfAccount.postJson", "/account/api/admin/attendance/sessions/list",
                new GridQuery { Top = 200 });
            if (env is { Success: true, Data: not null })
            {
                // No IsActive filter here: the endpoint scopes itself to active
                // sessions server-side, where no request can widen it. The old
                // client-side Where was filtering a list that had already been
                // filtered.
                _sessions = env.Data.Items.OrderBy(s => s.Code).ToList();
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

    private async Task OnSessionChangedAsync(SessionAttendanceRow? session)
    {
        // Clear any stale toast / prior hall so session A's data never bleeds into B.
        _toast = null;
        _selected = session;
        _map = null;
        _present = Array.Empty<SessionPresentAttendee>();
        _presentTotal = 0;
        // QA B17 — always tear the old loop down first: switching sessions must
        // not leave the previous session's timer running.
        StopAutoRefresh();
        if (session is not null)
        {
            await LoadHallAsync(interactive: true);
            StartAutoRefresh();
        }
    }

    private async Task RefreshAsync()
    {
        _toast = null;
        await LoadHallAsync(interactive: true);
    }

    // QA B17 — start a fresh poll loop for the currently-selected session. The
    // timer instance is handed to the loop so a restart's old loop can never
    // touch (or dispose-race) the new one.
    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        var timer = new PeriodicTimer(RefreshInterval);
        var cts = new CancellationTokenSource();
        _timer = timer;
        _cts = cts;
        _ = AutoRefreshLoopAsync(timer, cts.Token);
    }

    // QA B17 — cancel + dispose the loop's timer/CTS. Safe to call repeatedly and
    // when nothing is running; called on session change and on Dispose so a
    // Blazor Server circuit never keeps a timer alive after teardown.
    private void StopAutoRefresh()
    {
        _cts?.Cancel();
        _timer?.Dispose();
        _cts?.Dispose();
        _timer = null;
        _cts = null;
    }

    private async Task AutoRefreshLoopAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                await InvokeAsync(async () =>
                {
                    await LoadHallAsync(interactive: false);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The selection changed or the page was disposed; stop polling.
        }
        catch (ObjectDisposedException)
        {
            // The timer was disposed mid-tick by StopAutoRefresh; stop polling.
        }
        catch (Exception)
        {
            // The render circuit was torn down mid-refresh (LoadHallAsync handles
            // its own transient failures), so stop polling.
        }
    }

    /// <summary>Re-pulls the selected session's seat map + present list.
    /// <paramref name="interactive"/> false is the background tick: it must not
    /// flip the busy flag (that would disable the picker and spin the Refresh
    /// button every 15 seconds).</summary>
    private async Task LoadHallAsync(bool interactive)
    {
        if (_selected is not { } session || _inFlight) { return; }
        _inFlight = true;
        _busy = interactive;
        try
        {
            var mapEnv = await JS.InvokeAsync<ApiResult<SessionSeatMap>>(
                "simfAccount.getJson",
                $"/account/api/admin/sessions/{session.SessionId}/seat-map");
            var presentEnv = await JS.InvokeAsync<ApiResult<GridPage<SessionPresentAttendee>>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{session.SessionId}/present/list",
                new GridQuery { Top = PresentPageSize });

            // The admin may have switched sessions while these were in flight —
            // dropping the stale response keeps session A's hall off session B.
            if (_selected?.SessionId != session.SessionId) { return; }

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
                _present = presentEnv.Data.Items;
                // The server's count over the whole roster, not this page's row
                // count: the two differ exactly when the hall holds more than one
                // page, which is the case the summary line has to make visible.
                _presentTotal = presentEnv.Data.Total;
            }
            else
            {
                // The roster and the seat map fail independently. Surface the
                // failure (don't silently show an empty hall) and clear the list so
                // a failed Refresh can't leave a stale, misleading roster on screen.
                _present = Array.Empty<SessionPresentAttendee>();
                _presentTotal = 0;
                _toast = new Toast("error",
                    presentEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionLiveHall.LoadFailed"]);
            }
        }
        catch (Exception)
        {
            // QA B17 — a transient transport failure must not kill the background
            // poll; show the toast and let the next tick retry.
            _toast = new Toast("error", L["Admin.SessionLiveHall.LoadFailed"]);
        }
        finally
        {
            _inFlight = false;
            _busy = false;
        }
    }

    public void Dispose() => StopAutoRefresh();

    // -- Seat-map rendering (read-only 4-state grid) -------------------------

    // Seats in row i: the per-row SeatCounts entry when the layout is
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

    private static string Entered(DateTime enter) =>
        enter.FormatSaudi("dd-MM-yyyy hh:mm tt");

    private string MethodLabel(AttendanceMethod method) => method switch
    {
        AttendanceMethod.QrScan => L["Admin.SessionLiveHall.Method.QrScan"],
        AttendanceMethod.Geofence => L["Admin.SessionLiveHall.Method.Geofence"],
        _ => method.ToString(),
    };
}
