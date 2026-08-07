using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionSeatPlan
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummary> _sessions = new();
    private Guid? _selectedSessionId;
    private List<SeatPlanCell> _reservations = new();
    // DEF-SEA-001 — the reservation the admin has asked to release, held until the
    // SimfConfirm dialog names its holder and they confirm. Null = nothing pending.
    private SeatPlanCell? _releasing;
    // The hall layout for the selected session's hall, used to
    // paint the full grid (free + reserved). Null when the hall has no layout.
    private HallSeatLayoutSnapshot? _layout;
    private string _reserveRowLabel = string.Empty;
    // The manual guest note the admin types before
    // blocking a VVIP seat. A VVIP seat has no registration, so this free text IS
    // the occupant record ("هذا المقعد محجوز لمعالي الوزير") the app and the staff
    // seating desk display. Cleared after each successful block.
    private string _guestHintArabic = string.Empty;
    private string _guestHint = string.Empty;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // The tier of row i, read off the layout snapshot. Tolerant of an
    // absent/short SeatTiers (an older payload) → Normal, matching the server.
    private SeatTier TierOfRow(int rowIndex) =>
        _layout?.SeatTiers is { Count: > 0 } tiers && rowIndex < tiers.Count
            ? tiers[rowIndex]
            : SeatTier.Normal;

    private static string TierModifier(SeatTier tier) => tier switch
    {
        SeatTier.Vvip => "vvip",
        SeatTier.Vip => "vip",
        _ => "normal",
    };

    private string TierLabel(SeatTier tier) => tier switch
    {
        SeatTier.Vvip => L["Admin.HallSeatLayouts.Tier.Vvip"],
        SeatTier.Vip => L["Admin.HallSeatLayouts.Tier.Vip"],
        _ => L["Admin.HallSeatLayouts.Tier.Normal"],
    };

    // Seats in row i: the per-row SeatCounts entry when the layout is
    // variable (ragged), else the uniform SeatsPerRow. Tolerant of a short/absent
    // SeatCounts so a length-mismatched payload still renders.
    private int SeatsInRow(int rowIndex) => _layout is null ? 0
        : (_layout.SeatCounts is { Count: > 0 } sc && rowIndex < sc.Count
            ? sc[rowIndex]
            : _layout.SeatsPerRow);

    // Fast (row,seat) -> reservation lookup so the grid renders O(1) per seat.
    private SeatPlanCell? FindReservation(string rowLabel, int seatNumber) =>
        _reservations.FirstOrDefault(r =>
            string.Equals(r.RowLabel, rowLabel, StringComparison.OrdinalIgnoreCase)
            && r.SeatNumber == seatNumber);

    private static string SeatStateClass(SeatPlanCell? cell) => cell?.Kind switch
    {
        SeatReservationKind.UserBooking => "seatgrid__seat--user seatgrid__seat--reserved",
        SeatReservationKind.AdminReservedRow => "seatgrid__seat--admin seatgrid__seat--reserved",
        SeatReservationKind.RandomAssignment => "seatgrid__seat--random seatgrid__seat--reserved",
        _ => string.Empty,
    };

    /// <summary>DEF-SEA-001 — WHO holds this seat, in one line: the attendee's name
    /// for a real booking, the admin's manual guest note for a VVIP protocol block,
    /// and a plain "admin block" label when neither exists. The seat plan is an
    /// admin-only surface (gated <c>SeatPlans.View</c>), so naming the holder here
    /// is correct — this projection must never be copied onto an app/public read,
    /// which is why the identity lives on <c>SeatPlanCell</c> and not on the shared
    /// <c>SessionSeatCell</c>.</summary>
    private string HolderLabel(SeatPlanCell cell)
    {
        var arabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var name = InCurrentLanguage(cell.HolderName, cell.HolderNameArabic, arabic);
        if (name.Length > 0)
        {
            return name;
        }
        // A VVIP block has no registration, so the admin's guest note IS the
        // occupant record.
        var hint = InCurrentLanguage(cell.GuestHint, cell.GuestHintArabic, arabic);
        return hint.Length > 0 ? hint : L["Admin.SessionSeatPlans.Holder.AdminBlock"];
    }

    // The current language's text, falling back to the other language when it is
    // blank (an attendee may have only one of the two names filled in).
    private static string InCurrentLanguage(
        string? english, string? arabic, bool preferArabic)
    {
        var primary = preferArabic ? arabic : english;
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }
        var fallback = preferArabic ? english : arabic;
        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;
    }

    /// <summary>The seat's live state, now that the projection carries the
    /// real <c>CheckedIn</c> flag instead of the record default. Same three
    /// admin-visible states as the live-hall monitor: an admin block is
    /// "unavailable", a holder who has scanned in at the gate is "confirmed", and a
    /// holder who has not is "reserved".</summary>
    private string StateLabel(SeatPlanCell cell)
    {
        if (cell.Kind == SeatReservationKind.AdminReservedRow)
        {
            return L["Admin.SessionSeatPlans.State.Unavailable"];
        }
        return cell.CheckedIn
            ? L["Admin.SessionSeatPlans.State.Confirmed"]
            : L["Admin.SessionSeatPlans.State.Reserved"];
    }

    // The seat coordinates as the admin reads them ("B7"), or the general-admission
    // label for an OpenSeating join that holds no specific seat.
    private string SeatCoords(SeatPlanCell cell) =>
        cell.RowLabel is not null && cell.SeatNumber is not null
            ? $"{cell.RowLabel}{cell.SeatNumber}"
            : L["Admin.SessionSeatPlans.OpenSeating"];

    private string SeatTitle(string rowLabel, int seatNumber, SeatPlanCell? cell)
    {
        var coords = $"{rowLabel}{seatNumber}";
        if (cell is null)
        {
            return string.Format(L["Admin.SessionSeatPlans.Seat.FreeTitle"], coords);
        }
        // DEF-SEA-001 — the tooltip names the holder (attendee name, or the
        // VVIP guest note) so an admin can tell whose seat this is before clicking.
        return string.Format(
            L["Admin.SessionSeatPlans.Seat.ReservedTitle"],
            coords, cell.Kind, HolderLabel(cell));
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
        // DEF-SEA-001 — and drop any armed release: confirming it after the switch
        // would release a seat on the session the admin just left.
        _releasing = null;
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<SeatPlanCell>>>(
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

    // Fetch the hall layout for the selected session's hall so
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

    private void OnGuestHintArabicChanged(ChangeEventArgs e) =>
        _guestHintArabic = (e.Value?.ToString() ?? string.Empty).Trim();

    private void OnGuestHintChanged(ChangeEventArgs e) =>
        _guestHint = (e.Value?.ToString() ?? string.Empty).Trim();

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
                new AdminReserveSeatRequest
                {
                    RowLabel = rowLabel,
                    SeatNumber = seatNumber,
                    // The manual guest note typed above the grid.
                    GuestHint = _guestHint,
                    GuestHintArabic = _guestHintArabic,
                });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SessionSeatPlans.ReserveSeat.Done"]);
                _guestHint = string.Empty;
                _guestHintArabic = string.Empty;
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

    /// <summary>A seat click means "reserve" on a free seat (non-destructive, one
    /// tap) and "ask to release" on a held one (DEF-SEA-001 — the destructive path
    /// must be confirmed first).</summary>
    private Task OnSeatClickAsync(string rowLabel, int seatNumber, SeatPlanCell? cell)
    {
        if (cell is null)
        {
            return ReserveSeatAsync(rowLabel, seatNumber);
        }
        AskRelease(cell);
        return Task.CompletedTask;
    }

    /// <summary>DEF-SEA-001 — releasing a held seat used to fire on a single click,
    /// with nothing on screen saying whose seat it was and no way to undo it. The
    /// click now only ARMS the release: <see cref="_releasing"/> opens the shared
    /// <c>SimfConfirm</c> dialog, which names the holder and the seat before the
    /// destructive call runs.</summary>
    private void AskRelease(SeatPlanCell row)
    {
        if (_selectedSessionId is null || _busy) return;
        _toast = null;
        _releasing = row;
    }

    private void CancelRelease() => _releasing = null;

    // The confirmation question, naming the holder and the seat so the admin cannot
    // release a live attendee's seat without reading who it belongs to.
    private string ReleaseConfirmMessage() => _releasing is null
        ? string.Empty
        : string.Format(
            L["Admin.SessionSeatPlans.Release.Message"],
            SeatCoords(_releasing), HolderLabel(_releasing));

    private async Task ConfirmReleaseAsync()
    {
        if (_releasing is { } row)
        {
            await ReleaseAsync(row);
        }
    }

    private async Task ReleaseAsync(SeatPlanCell row)
    {
        if (_selectedSessionId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/seats/{row.ReservationId}");
            // Close the confirm first so a failure lands on the visible page body,
            // not behind the still-open overlay (matches the CRUD delete pattern).
            _releasing = null;
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
