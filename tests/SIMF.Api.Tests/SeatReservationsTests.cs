// D-175 (gap doc G11, Mockup page 7) — per-session seat reservations.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.SeatReservations;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SeatReservationsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SeatReservationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Visitor_can_self_pick_then_release_their_seat()
    {
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 3 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal("A", mine.RowLabel);
        Assert.Equal(3, mine.SeatNumber);
        Assert.Equal(SeatReservationKind.UserBooking, mine.Kind);

        var release = await DeleteAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/mine", visitor);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        // After release, picking the same seat again should succeed.
        var rePick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 3 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, rePick.StatusCode);
    }

    [Fact]
    public async Task Reserving_a_seat_auto_confirms_without_a_notification()
    {
        // 2026-07-18 (reservation-only) — a fresh self-pick is confirmed on create
        // (no CP approval step) and nothing is dispatched: the app shows the
        // reserve-success message inline, not a push notification.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(BookingStatus.Approved, mine.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var note = await db.Notifications
            .SingleOrDefaultAsync(n => n.Kind == NotificationKind.BookingConfirmed
                && n.RelatedEntityId == session.Id);
        Assert.Null(note); // reservation-only fires no notification on reserve
    }

    [Fact]
    public async Task Second_visitor_cannot_take_a_seat_already_taken()
    {
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var v1 = await SignInApprovedVisitorAsync();
        var v2 = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v2);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatAlreadyReserved, body.Error!.Code);
    }

    [Fact]
    public async Task User_cannot_hold_two_seats_in_the_same_session()
    {
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatAlreadyOwnedBySession, body.Error!.Code);
    }

    [Fact]
    public async Task Random_allocator_picks_a_free_seat()
    {
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 2);
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve-random",
            new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = (await response.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(SeatReservationKind.RandomAssignment, mine.Kind);
        Assert.Equal("A", mine.RowLabel);
        Assert.InRange(mine.SeatNumber!.Value, 1, 2);
    }

    [Fact]
    public async Task Capacity_cap_blocks_overbooking()
    {
        // Layout 1x1 — session capacity is 1, then the second visitor should fail.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 1);
        var v1 = await SignInApprovedVisitorAsync();
        var v2 = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve-random",
            new { }, v2);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatSessionFull, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_can_reserve_an_entire_row()
    {
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 3);
        var admin = await CreateAdministratorAndSignInAsync();

        var block = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-row",
            new AdminReserveRowRequest { RowLabel = "A" }, admin);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var visitor = await SignInApprovedVisitorAsync();
        var attempt = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, attempt.StatusCode);

        // Row B is still free.
        var freeRow = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, freeRow.StatusCode);
    }

    [Fact]
    public async Task Admin_can_reserve_a_single_seat_for_a_vip()
    {
        // 2026-07-18 — an admin reserves ONE specific seat for a VIP (a single admin
        // block). A visitor then cannot book that seat, but its neighbour stays free,
        // and reserving the same seat twice is a conflict.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var admin = await CreateAdministratorAndSignInAsync();

        var block = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-seat",
            new AdminReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, admin);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        // Reserving the same seat again is a conflict.
        var again = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-seat",
            new AdminReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, admin);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var visitor = await SignInApprovedVisitorAsync();
        // The blocked seat A2 is refused for a visitor.
        var taken = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var body = (await taken.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatAlreadyReserved, body.Error!.Code);

        // The neighbouring seat A1 is still free.
        var free = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
    }

    [Fact]
    public async Task Admin_reserve_seat_out_of_bounds_is_400()
    {
        // A seat beyond the row width (or a row absent from the layout) is a 400.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var admin = await CreateAdministratorAndSignInAsync();

        var bad = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-seat",
            new AdminReserveSeatRequest { RowLabel = "A", SeatNumber = 9 }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = (await bad.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatOutOfBounds, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_layout_capacity_above_hall_capacity_is_400()
    {
        var hall = await SeedHallAsync(capacity: 5);
        var admin = await CreateAdministratorAndSignInAsync();

        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B", "C" }, SeatsPerRow = 5,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = (await put.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatCapacityExceeded, body.Error!.Code);
    }

    [Fact]
    public async Task Seat_map_returns_my_cell_for_the_reserver()
    {
        // Page_017 (Session detail) — the مقعدي card reads SessionSeatMap.MyCell.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 4 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var map = await GetAuthAsync($"/api/v1/app/sessions/{session.Id}/seats", visitor);
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        var seatMap = (await map.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;

        Assert.NotNull(seatMap.MyCell);
        Assert.Equal("B", seatMap.MyCell!.RowLabel);
        Assert.Equal(4, seatMap.MyCell.SeatNumber);
        Assert.Equal(SeatReservationKind.UserBooking, seatMap.MyCell.Kind);
        Assert.Contains(seatMap.ReservedCells, c => c.RowLabel == "B" && c.SeatNumber == 4);
    }

    [Fact]
    public async Task Seat_map_my_cell_carries_the_approved_booking_status()
    {
        // 2026-07-18 (reservation-only) — a reservation is confirmed (Approved) the
        // moment it is made, so the seat map's MyCell carries Approved immediately
        // and CheckedIn is false until the visitor checks in at the hall gate.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var map = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats", visitor))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.NotNull(map.MyCell);
        Assert.Equal(BookingStatus.Approved, map.MyCell!.Status);
        Assert.False(map.MyCell.CheckedIn); // reserved, not yet checked in at the gate
    }

    [Fact]
    public async Task Seat_map_my_cell_is_null_for_a_caller_without_a_reservation()
    {
        // Page_017 — a signed-in approved account with no booking sees no card:
        // MyCell is null even though another visitor's seat shows in the grid.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var booker = await SignInApprovedVisitorAsync();
        var onlooker = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, booker);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var map = await GetAuthAsync($"/api/v1/app/sessions/{session.Id}/seats", onlooker);
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        var seatMap = (await map.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;

        Assert.Null(seatMap.MyCell);
        Assert.Contains(seatMap.ReservedCells, c => c.RowLabel == "A" && c.SeatNumber == 2);
    }

    [Fact]
    public async Task Seat_map_requires_an_approved_account()
    {
        // Page_017 — the my-seat card is login-only: the anonymous detail (screen
        // 17) renders, but the seat endpoint rejects an unauthenticated caller, so
        // a guest sees no card.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);

        var anon = await _client.GetAsync($"/api/v1/app/sessions/{session.Id}/seats");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    [Fact]
    public async Task Seat_map_returns_the_layout_and_blocked_row_to_a_viewer()
    {
        // Page_018 (My Seat map) — the grid is drawn from RowLabels + SeatsPerRow,
        // and an admin-blocked row + another visitor's seat must both come back as
        // reserved cells (with the right Kind) so the grid colours them محجوز.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var admin = await CreateAdministratorAndSignInAsync();
        var booker = await SignInApprovedVisitorAsync();
        var viewer = await SignInApprovedVisitorAsync();

        var block = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-row",
            new AdminReserveRowRequest { RowLabel = "A" }, admin);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 4 }, booker);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var map = await GetAuthAsync($"/api/v1/app/sessions/{session.Id}/seats", viewer);
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        var seatMap = (await map.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;

        // Grid dimensions the app draws from.
        Assert.Equal(new[] { "A", "B" }, seatMap.RowLabels);
        Assert.Equal(5, seatMap.SeatsPerRow);

        // The whole blocked row A is materialised as AdminReservedRow cells.
        for (var seat = 1; seat <= 5; seat++)
        {
            Assert.Contains(seatMap.ReservedCells, c =>
                c.RowLabel == "A" && c.SeatNumber == seat
                && c.Kind == SeatReservationKind.AdminReservedRow);
        }

        // The other visitor's seat is a reserved UserBooking cell.
        Assert.Contains(seatMap.ReservedCells, c =>
            c.RowLabel == "B" && c.SeatNumber == 4
            && c.Kind == SeatReservationKind.UserBooking);

        // 5 blocked + 1 booked; the viewer holds none.
        Assert.Equal(6, seatMap.ActiveReservedCount);
        Assert.Null(seatMap.MyCell);
    }

    [Fact]
    public async Task Visitor_can_join_an_open_seating_session_without_a_seat()
    {
        // D-485 — an open-seating (general-admission) session has no seat grid;
        // the visitor just joins and gets a confirmed reservation with no seat.
        var session = await SeedOpenSeatingSessionAsync(capacity: 50);
        var visitor = await SignInApprovedVisitorAsync();

        var join = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var mine = (await join.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(SeatReservationKind.OpenSeating, mine.Kind);
        Assert.Null(mine.RowLabel);
        Assert.Null(mine.SeatNumber);
        Assert.Equal(BookingStatus.Approved, mine.Status);
    }

    [Fact]
    public async Task Open_seating_join_is_one_per_session()
    {
        var session = await SeedOpenSeatingSessionAsync(capacity: 50);
        var visitor = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatAlreadyOwnedBySession, body.Error!.Code);
    }

    [Fact]
    public async Task Joining_an_assigned_seat_session_is_rejected()
    {
        // D-485 — /join is only for open-seating sessions; an assigned-seat session
        // tells the app to show the seat picker instead (SEAT_SELECTION_REQUIRED).
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var join = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
        var body = (await join.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatSelectionRequired, body.Error!.Code);
    }

    [Fact]
    public async Task Picking_a_seat_when_the_session_overrides_to_open_seating_is_rejected()
    {
        // D-485 — a session can override its assigned-seat hall to open seating;
        // a seat-pick on it is then rejected with OPEN_SEATING_ONLY.
        var (session, _) = await SeedSessionWithLayoutAsync(
            new[] { "A" }, seatsPerRow: 3,
            sessionModeOverride: SeatSelectionMode.OpenSeating);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
        var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OpenSeatingOnly, body.Error!.Code);
    }

    [Fact]
    public async Task Join_succeeds_on_an_assigned_seat_session_that_has_no_layout()
    {
        // D-706 — the seeded-prod shape: a hall left on the AssignedSeat default
        // with NO seat layout. With no seats to assign it is treated as open
        // seating: the seat map reports OpenSeating (so the app shows a one-tap
        // join, not an empty picker) and /join is accepted. This is the fix for the
        // owner's "join session not working".
        var session = await SeedAssignedSeatNoLayoutSessionAsync(capacity: 50);
        var visitor = await SignInApprovedVisitorAsync();

        var mapResp = await GetAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats", visitor);
        Assert.Equal(HttpStatusCode.OK, mapResp.StatusCode);
        var seatMap = (await mapResp.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.Equal(SeatSelectionMode.OpenSeating, seatMap.Mode);
        Assert.Empty(seatMap.RowLabels);

        var join = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var mine = (await join.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(SeatReservationKind.OpenSeating, mine.Kind);
        Assert.Equal(BookingStatus.Approved, mine.Status);
    }

    // -- M-2: declared-capacity backstop -------------------------------------

    [Fact]
    public async Task Capacity_override_below_layout_blocks_the_over_cap_reserve()
    {
        // M-2 — the effective cap is min(layout, CapacityOverride) = 1, so the
        // second seat pick is blocked even though the layout has five seats.
        var (session, _) = await SeedSessionWithLayoutAsync(
            new[] { "A" }, seatsPerRow: 5, capacityOverride: 1);
        var v1 = await SignInApprovedVisitorAsync();
        var v2 = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, v2);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatSessionFull, body.Error!.Code);
    }

    [Fact]
    public async Task Concurrent_reserve_random_never_exceeds_capacity_override()
    {
        // #21 — layout A×5 (Hall.Capacity 5) but CapacityOverride 2. Five visitors
        // race reserve-random. The serializable count-gate + insert (via the execution
        // strategy) makes the outcome EXACT under true parallelism: precisely `cap`
        // succeed (no over-reject — a deadlock victim re-runs and fills a real free
        // seat) and never more than `cap` (no oversell — the key-range lock serialises
        // count-then-insert). Every loser is a clean 409 SeatSessionFull.
        const int cap = 2;
        var (session, _) = await SeedSessionWithLayoutAsync(
            new[] { "A" }, seatsPerRow: 5, capacityOverride: cap);
        var visitors = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            visitors.Add(await SignInApprovedVisitorAsync());
        }

        var responses = await Task.WhenAll(visitors.Select(v =>
            PostAuthAsync<object>(
                $"/api/v1/app/sessions/{session.Id}/seats/reserve-random", new { }, v)));

        var success = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(cap, success);
        foreach (var r in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            var body = (await r.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.SeatSessionFull, body.Error!.Code);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var active = await db.SeatReservations
            .CountAsync(r => r.SessionId == session.Id && r.ReleasedAt == null);
        Assert.Equal(cap, active);
        Assert.Equal(success, active);
    }

    // -- M-1: open-seating join capacity backstop ------------------------------

    [Fact]
    public async Task Open_seating_join_capacity_is_enforced_under_concurrency()
    {
        // #21 — six visitors race the open-seating join on a cap-2 session. Open
        // seating has no per-seat key, so the serializable count-gate + insert (via
        // the execution strategy) IS the only backstop — and it is exact: precisely
        // `cap` succeed (no over-reject) and never more (no oversell); every loser is
        // a clean 409 SeatSessionFull.
        const int cap = 2;
        var session = await SeedOpenSeatingSessionAsync(capacity: cap);
        var visitors = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            visitors.Add(await SignInApprovedVisitorAsync());
        }

        var responses = await Task.WhenAll(visitors.Select(v =>
            PostAuthAsync<object>(
                $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, v)));

        var success = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(cap, success);
        foreach (var r in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            var body = (await r.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.SeatSessionFull, body.Error!.Code);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var active = await db.SeatReservations
            .CountAsync(r => r.SessionId == session.Id && r.ReleasedAt == null);
        Assert.Equal(cap, active);
        Assert.Equal(success, active);
    }

    // -- M-4: admin release closes the lifecycle + notifies --------------------

    [Fact]
    public async Task Admin_release_marks_cancelled_and_notifies()
    {
        // M-4 — releasing a confirmed booking now sets a terminal Cancelled status,
        // stamps the reviewer, and dispatches a BookingReleased notification.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var reservationId = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!.ReservationId;

        // Approve directly so the release closes a CONFIRMED booking.
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.SeatReservations.SingleAsync(r => r.Id == reservationId);
            row.Status = BookingStatus.Approved;
            await db.SaveChangesAsync();
        }

        var adminId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
            await svc.AdminReleaseAsync(adminId, session.Id, reservationId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.SeatReservations.SingleAsync(r => r.Id == reservationId);
            Assert.NotNull(row.ReleasedAt);
            Assert.Equal(BookingStatus.Cancelled, row.Status);
            Assert.Equal(adminId, row.ReviewedByUserId);

            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.BookingReleased
                && n.RelatedEntityId == session.Id);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Admin_release_of_admin_reserved_row_does_not_notify()
    {
        // M-4 — an admin block has no attendee (ReservedForUserId null), so releasing
        // it sets Cancelled but dispatches no BookingReleased notification.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var admin = await CreateAdministratorAndSignInAsync();
        var block = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-row",
            new AdminReserveRowRequest { RowLabel = "A" }, admin);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        Guid reservationId;
        using (var seed = _factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            reservationId = await db.SeatReservations
                .Where(r => r.SessionId == session.Id
                    && r.Kind == SeatReservationKind.AdminReservedRow)
                .Select(r => r.Id).FirstAsync();
        }

        var adminId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
            await svc.AdminReleaseAsync(adminId, session.Id, reservationId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.SeatReservations.SingleAsync(r => r.Id == reservationId);
            Assert.Equal(BookingStatus.Cancelled, row.Status);

            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.BookingReleased
                && n.RelatedEntityId == session.Id);
            Assert.Equal(0, count);
        }
    }

    // -- #6/#17: a visitor hold is stamped with the no-show release deadline ----

    [Fact]
    public async Task Reserving_stamps_the_no_show_deadline_on_the_hold()
    {
        // #6/#17 — a visitor seat pick is stamped with Expires = the session's
        // Start − 3min (the no-show release deadline); an admin-reserved row seat
        // never expires (Expires null).
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var reservationId = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!.ReservationId;

        var admin = await CreateAdministratorAndSignInAsync();
        var block = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/reserve-row",
            new AdminReserveRowRequest { RowLabel = "A" }, admin);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var mine = await db.SeatReservations.SingleAsync(r => r.Id == reservationId);
        Assert.NotNull(mine.Expires);
        var expected = session.Start - SeatReservationService.NoShowReleaseGrace;
        Assert.True(
            (mine.Expires!.Value - expected).Duration() < TimeSpan.FromSeconds(1),
            $"no-show deadline {mine.Expires} not ~ {expected}");

        var adminSeat = await db.SeatReservations
            .Where(r => r.SessionId == session.Id
                && r.Kind == SeatReservationKind.AdminReservedRow)
            .FirstAsync();
        Assert.Null(adminSeat.Expires);
    }

    // -- H-2: a layout change may not orphan active reservations ---------------

    [Fact]
    public async Task Shrinking_a_layout_that_orphans_a_reservation_is_blocked()
    {
        // H-2 — dropping row B while B4 is actively reserved is a 409, and the stored
        // layout is left unchanged.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 4 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest { RowLabels = new[] { "A" }, SeatsPerRow = 5 }, admin);
        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
        var body = (await put.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatLayoutHasReservations, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.HallSeatLayouts.Where(l => l.HallId == hall.Id)
            .Select(l => l.RowLabels).SingleAsync();
        Assert.Equal("A,B", rows);
    }

    [Fact]
    public async Task Shrinking_seats_per_row_below_a_booked_seat_is_blocked()
    {
        // H-2 — A5 is reserved; shrinking the row to 3 seats would strand it → 409.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 5 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest { RowLabels = new[] { "A" }, SeatsPerRow = 3 }, admin);
        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
        var body = (await put.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatLayoutHasReservations, body.Error!.Code);
    }

    [Fact]
    public async Task Layout_change_with_no_orphans_succeeds()
    {
        // H-2 — an active A1 stays inside the grid and a RELEASED B4 does not block,
        // so dropping row B succeeds.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var v1 = await SignInApprovedVisitorAsync();
        var a1 = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, a1.StatusCode);

        var v2 = await SignInApprovedVisitorAsync();
        var b4 = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 4 }, v2);
        Assert.Equal(HttpStatusCode.OK, b4.StatusCode);
        var release = await DeleteAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/mine", v2);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest { RowLabels = new[] { "A" }, SeatsPerRow = 5 }, admin);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.HallSeatLayouts.Where(l => l.HallId == hall.Id)
            .Select(l => l.RowLabels).SingleAsync();
        Assert.Equal("A", rows);
    }

    // -- D-767: per-row variable seat counts ----------------------------------

    [Fact]
    public async Task Variable_layout_bounds_each_row_by_its_own_seat_count()
    {
        // D-767 — a ragged layout (VIP=2, A=10, B=8) bounds each row by ITS own
        // count: seat 10 is the last real seat of the 10-seat row A (allowed), but
        // seat 10 in the 8-seat row B is out of bounds → 400 SEAT_OUT_OF_BOUNDS.
        var (session, _) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A", "B" }, new[] { 2, 10, 8 });

        var v1 = await SignInApprovedVisitorAsync();
        var okA = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 10 }, v1);
        Assert.Equal(HttpStatusCode.OK, okA.StatusCode);

        var v2 = await SignInApprovedVisitorAsync();
        var badB = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 10 }, v2);
        Assert.Equal(HttpStatusCode.BadRequest, badB.StatusCode);
        var body = (await badB.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatOutOfBounds, body.Error!.Code);
    }

    [Fact]
    public async Task Variable_layout_capacity_equals_the_sum_of_the_row_counts()
    {
        // D-767 — the session's capacity is sum(counts) = 1 + 2 = 3, NOT
        // rows × max(counts) (= 4) and not the roomy hall (100). Fill all three real
        // seats, then a fourth booking is full → 409 SEAT_SESSION_FULL.
        var (session, _) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A" }, new[] { 1, 2 }, hallCapacity: 100);

        foreach (var (row, seat) in new[] { ("VIP", 1), ("A", 1), ("A", 2) })
        {
            var v = await SignInApprovedVisitorAsync();
            var ok = await PostAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats/reserve",
                new ReserveSeatRequest { RowLabel = row, SeatNumber = seat }, v);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var overflow = await SignInApprovedVisitorAsync();
        var full = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, overflow);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        var body = (await full.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatSessionFull, body.Error!.Code);
    }

    [Fact]
    public async Task Random_allocator_never_returns_a_seat_beyond_a_short_rows_count()
    {
        // D-767 — the row-major random scan must stop each row at ITS own count. With
        // the 2-seat VIP row filled, the allocator has to skip to row A (1..8) rather
        // than hand out a phantom VIP3 (which a uniform max-width scan would).
        var (session, _) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A" }, new[] { 2, 8 });

        foreach (var seat in new[] { 1, 2 })
        {
            var v = await SignInApprovedVisitorAsync();
            var ok = await PostAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats/reserve",
                new ReserveSeatRequest { RowLabel = "VIP", SeatNumber = seat }, v);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var picker = await SignInApprovedVisitorAsync();
        var random = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve-random", new { }, picker);
        Assert.Equal(HttpStatusCode.OK, random.StatusCode);
        var mine = (await random.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(SeatReservationKind.RandomAssignment, mine.Kind);
        Assert.Equal("A", mine.RowLabel);
        Assert.InRange(mine.SeatNumber!.Value, 1, 8);
    }

    [Fact]
    public async Task Shrinking_a_variable_row_below_a_booked_seat_is_blocked()
    {
        // D-767 — B8 is booked; shrinking row B from 8 to 5 seats would strand it, so
        // the per-row orphan guard rejects the change → 409 SEAT_LAYOUT_HAS_RESERVATIONS.
        var (session, hall) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A", "B" }, new[] { 2, 10, 8 });
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 8 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "VIP", "A", "B" },
                SeatCounts = new[] { 2, 10, 5 },
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
        var body = (await put.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatLayoutHasReservations, body.Error!.Code);
    }

    [Fact]
    public async Task Shrinking_a_variable_row_above_its_booked_seats_succeeds()
    {
        // D-767 — B2 is booked and stays inside the shrunk grid (2 <= 5), so reducing
        // row B from 8 to 5 seats is allowed → 200, and the snapshot reads the new counts.
        var (session, hall) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A", "B" }, new[] { 2, 10, 8 });
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 2 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "VIP", "A", "B" },
                SeatCounts = new[] { 2, 10, 5 },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var snap = (await put.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Equal(new[] { 2, 10, 5 }, snap.SeatCounts);
        Assert.Equal(17, snap.LayoutCapacity);
    }

    [Fact]
    public async Task Uniform_layout_set_with_null_seat_counts_uses_rows_times_seats_per_row()
    {
        // D-767 back-compat — a layout with no SeatCounts stays uniform: the capacity is
        // rows × SeatsPerRow (3 × 4 = 12) and the wire key is omitted (null), exactly as
        // before the per-row feature.
        var hall = await SeedHallAsync(capacity: 30);
        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B", "C" }, SeatsPerRow = 4,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var snap = (await put.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Null(snap.SeatCounts);
        Assert.Equal(4, snap.SeatsPerRow);
        Assert.Equal(12, snap.LayoutCapacity);
    }

    [Fact]
    public async Task Admin_set_variable_layout_round_trips_the_seat_counts()
    {
        // D-767 — setting counts 2,10,8 stores them and reports sum = 20 with the
        // uniform fallback SeatsPerRow = max = 10; a subsequent GET reads back the same.
        var hall = await SeedHallAsync(capacity: 30);
        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "VIP", "A", "B" },
                SeatCounts = new[] { 2, 10, 8 },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var setSnap = (await put.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Equal(new[] { 2, 10, 8 }, setSnap.SeatCounts);
        Assert.Equal(20, setSnap.LayoutCapacity);
        Assert.Equal(10, setSnap.SeatsPerRow);

        var get = await GetAuthAsync($"/api/v1/admin/halls/{hall.Id}/seat-layout", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var getSnap = (await get.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Equal(new[] { "VIP", "A", "B" }, getSnap.RowLabels);
        Assert.Equal(new[] { 2, 10, 8 }, getSnap.SeatCounts);
        Assert.Equal(20, getSnap.LayoutCapacity);
    }

    [Fact]
    public async Task Set_layout_with_a_count_mismatch_is_400()
    {
        // D-767 — the SeatCounts length MUST equal the row count; 2 counts for 3 rows
        // is invalid → 400 SEAT_LAYOUT_INVALID.
        var hall = await SeedHallAsync(capacity: 30);
        var admin = await CreateAdministratorAndSignInAsync();
        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B", "C" },
                SeatCounts = new[] { 1, 2 },
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = (await put.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatLayoutInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Set_layout_rejects_out_of_range_counts_and_an_over_capacity_sum()
    {
        // D-767 — per-row range + total-capacity guards: a count of 0 or 81 is outside
        // 1..80 → 400 SEAT_LAYOUT_INVALID; in-range counts whose SUM exceeds the hall
        // capacity → 400 SEAT_CAPACITY_EXCEEDED.
        var admin = await CreateAdministratorAndSignInAsync();
        var roomy = await SeedHallAsync(capacity: 200);

        var zero = await PutAuthAsync(
            $"/api/v1/admin/halls/{roomy.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B" }, SeatCounts = new[] { 0, 5 },
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(ErrorCodes.SeatLayoutInvalid,
            (await zero.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);

        var tooMany = await PutAuthAsync(
            $"/api/v1/admin/halls/{roomy.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B" }, SeatCounts = new[] { 81, 5 },
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(ErrorCodes.SeatLayoutInvalid,
            (await tooMany.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);

        var tight = await SeedHallAsync(capacity: 5);
        var overCap = await PutAuthAsync(
            $"/api/v1/admin/halls/{tight.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "A", "B" }, SeatCounts = new[] { 4, 4 },
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, overCap.StatusCode);
        Assert.Equal(ErrorCodes.SeatCapacityExceeded,
            (await overCap.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);
    }

    [Fact]
    public async Task Seat_map_emits_seat_counts_for_a_variable_layout_and_null_for_uniform()
    {
        // D-767 wire back-compat — the app's seat map carries the per-row counts for a
        // ragged layout (with SeatsPerRow = max as the frozen uniform-width field) and
        // omits the key (null) for a uniform layout, so old and new apps both decode.
        var (variable, _) = await SeedSessionWithVariableLayoutAsync(
            new[] { "VIP", "A", "B" }, new[] { 2, 10, 8 });
        var (uniform, _) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var viewer = await SignInApprovedVisitorAsync();

        var vMap = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{variable.Id}/seats", viewer))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.Equal(new[] { "VIP", "A", "B" }, vMap.RowLabels);
        Assert.Equal(new[] { 2, 10, 8 }, vMap.SeatCounts);
        Assert.Equal(10, vMap.SeatsPerRow);

        var uMap = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{uniform.Id}/seats", viewer))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.Null(uMap.SeatCounts);
        Assert.Equal(5, uMap.SeatsPerRow);
    }

    // -- B15: a seat layout can be removed (back to general admission) --------

    [Fact]
    public async Task Deleting_a_layout_reverts_the_hall_to_general_admission()
    {
        // B15 — the layout can be removed entirely: the row is gone from the DB, the
        // read-back is empty, and the session's seat map now reports OpenSeating, so a
        // laid-out hall really can be converted back to general admission.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var admin = await CreateAdministratorAndSignInAsync();

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var after = (await delete.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Empty(after.RowLabels);
        Assert.Equal(0, after.SeatsPerRow);
        Assert.Equal(0, after.LayoutCapacity);
        Assert.Equal(hall.Capacity, after.HallCapacity);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            Assert.False(await db.HallSeatLayouts.AnyAsync(l => l.HallId == hall.Id));
        }

        var reread = (await (await GetAuthAsync(
                $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin))
            .Content.ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Empty(reread.RowLabels);

        // D-706 — no layout means no assignable seats, so the session is now joined
        // with one tap rather than by picking a seat.
        var viewer = await SignInApprovedVisitorAsync();
        var map = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats", viewer))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.Equal(SeatSelectionMode.OpenSeating, map.Mode);
    }

    [Fact]
    public async Task Deleting_a_layout_that_would_strand_a_reservation_is_blocked()
    {
        // B15 — the H-2 orphan rule applied to the hardest shrink: A2 is actively
        // reserved, so the delete is refused 409 and the layout survives intact.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SeatLayoutHasReservations, body.Error!.Code);
        // The message names how many reservations block it, in both languages.
        Assert.Contains("1", body.Error!.Message);
        Assert.Contains("1", body.Error!.MessageArabic);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.HallSeatLayouts.Where(l => l.HallId == hall.Id)
            .Select(l => l.RowLabels).SingleAsync();
        Assert.Equal("A", rows);
    }

    [Fact]
    public async Task Deleting_a_layout_ignores_released_reservations()
    {
        // B15 — only ACTIVE holds block the delete. A released seat is not stranded,
        // so the removal goes through.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var visitor = await SignInApprovedVisitorAsync();
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var release = await DeleteAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/mine", visitor);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        var admin = await CreateAdministratorAndSignInAsync();
        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.HallSeatLayouts.AnyAsync(l => l.HallId == hall.Id));
    }

    [Fact]
    public async Task Deleting_a_layout_that_does_not_exist_is_a_404()
    {
        // B15 — a hall that was never laid out has nothing to remove; the delete is a
        // precise 404 rather than a silent success.
        var hall = await SeedHallAsync(capacity: 30);
        var admin = await CreateAdministratorAndSignInAsync();

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(ErrorCodes.SeatLayoutMissing,
            (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!.Error!.Code);
    }

    [Fact]
    public async Task Deleting_a_layout_can_be_followed_by_defining_a_new_one()
    {
        // B15 — the round trip the operator actually needs: remove the grid, then lay
        // the hall out again from scratch.
        var (_, hall) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var admin = await CreateAdministratorAndSignInAsync();

        Assert.Equal(HttpStatusCode.OK, (await DeleteAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout", admin)).StatusCode);

        var put = await PutAuthAsync(
            $"/api/v1/admin/halls/{hall.Id}/seat-layout",
            new SetHallSeatLayoutRequest
            {
                RowLabels = new[] { "VIP", "A" },
                SeatCounts = new[] { 2, 6 },
                SeatTiers = new[] { SeatTier.Vvip, SeatTier.Normal },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var snapshot = (await put.Content
            .ReadFromJsonAsync<ApiResult<HallSeatLayoutSnapshot>>())!.Data!;
        Assert.Equal(new[] { "VIP", "A" }, snapshot.RowLabels);
        Assert.Equal(new[] { 2, 6 }, snapshot.SeatCounts);
        Assert.Equal(8, snapshot.LayoutCapacity);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(Session Session, Hall Hall)> SeedSessionWithLayoutAsync(
        string[] rowLabels, int seatsPerRow,
        SeatSelectionMode hallMode = SeatSelectionMode.AssignedSeat,
        SeatSelectionMode? sessionModeOverride = null,
        int? capacityOverride = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = rowLabels.Length * seatsPerRow,
            SeatSelectionMode = hallMode,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = string.Join(',', rowLabels),
            SeatsPerRow = seatsPerRow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live", TitleArabic = "مباشر",
            HallId = hall.Id,
            SeatSelectionModeOverride = sessionModeOverride,
            CapacityOverride = capacityOverride,
            // P2.2 — D-227: a FUTURE window so bookings can be cancelled before
            // the session starts (the new cancel-before-start guard, FR-504).
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
    }

    private async Task<(Session Session, Hall Hall)> SeedSessionWithVariableLayoutAsync(
        string[] rowLabels, int[] seatCounts,
        SeatSelectionMode hallMode = SeatSelectionMode.AssignedSeat,
        SeatSelectionMode? sessionModeOverride = null,
        int? capacityOverride = null,
        int? hallCapacity = null)
    {
        // D-767 — mirrors SeedSessionWithLayoutAsync but seeds a RAGGED layout: a
        // per-row SeatCounts CSV parallel to RowLabels, with SeatsPerRow storing
        // max(counts) as the uniform fallback (exactly what SetLayoutAsync persists).
        // The hall defaults to sum(counts) but can be overridden to test the
        // sum-over-capacity guard.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = hallCapacity ?? seatCounts.Sum(),
            SeatSelectionMode = hallMode,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = string.Join(',', rowLabels),
            SeatsPerRow = seatCounts.Max(),
            SeatCounts = string.Join(',', seatCounts),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live", TitleArabic = "مباشر",
            HallId = hall.Id,
            SeatSelectionModeOverride = sessionModeOverride,
            CapacityOverride = capacityOverride,
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
    }

    private async Task<Session> SeedOpenSeatingSessionAsync(int capacity)
    {
        // D-485 — an open-seating hall has NO seat layout; the session is joined
        // in bulk (general admission), capacity-bounded by Hall.Capacity.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = capacity,
            SeatSelectionMode = SeatSelectionMode.OpenSeating,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live", TitleArabic = "مباشر",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<Session> SeedAssignedSeatNoLayoutSessionAsync(int capacity)
    {
        // D-706 — a hall on the AssignedSeat default with NO HallSeatLayout (the
        // shape the content seeder produces). No seats to assign → open seating.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = capacity,
            SeatSelectionMode = SeatSelectionMode.AssignedSeat,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live", TitleArabic = "مباشر",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<Hall> SeedHallAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = capacity, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"sr-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sr-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "SR Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
