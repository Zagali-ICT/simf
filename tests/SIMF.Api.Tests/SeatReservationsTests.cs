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
    public async Task Reserving_a_seat_creates_a_pending_booking_without_a_confirmation()
    {
        // P2.2 — D-227: a fresh self-pick is Pending; the booking-confirmed
        // notification now fires on APPROVE (FDS-005 §5.2), not on reserve.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(BookingStatus.Pending, mine.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var note = await db.Notifications
            .SingleOrDefaultAsync(n => n.Kind == NotificationKind.BookingConfirmed
                && n.RelatedEntityId == session.Id);
        Assert.Null(note); // nothing dispatched until the CP approves
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
    public async Task Seat_map_my_cell_carries_the_booking_status_pending_then_approved()
    {
        // D-572 — the app switches the مقعدي hint on the booking status, so the
        // seat map's MyCell must carry it: Pending until the CP approves, then
        // Approved (the card then shows "show your badge at entry").
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 3);
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var reservation = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;

        // Fresh booking → MyCell is Pending.
        var pendingMap = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats", visitor))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.NotNull(pendingMap.MyCell);
        Assert.Equal(BookingStatus.Pending, pendingMap.MyCell!.Status);

        // Approve the booking directly; the map then reflects Approved.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var booking = await db.SeatReservations
                .SingleAsync(r => r.Id == reservation.ReservationId);
            booking.Status = BookingStatus.Approved;
            await db.SaveChangesAsync();
        }

        var approvedMap = (await (await GetAuthAsync(
                $"/api/v1/app/sessions/{session.Id}/seats", visitor))
            .Content.ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.NotNull(approvedMap.MyCell);
        Assert.Equal(BookingStatus.Approved, approvedMap.MyCell!.Status);
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
        // the visitor just joins and gets a Pending reservation with no seat.
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
        Assert.Equal(BookingStatus.Pending, mine.Status);
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
        Assert.Equal(BookingStatus.Pending, mine.Status);
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
        // M-2 — layout A×5 (Hall.Capacity 5) but CapacityOverride 2. Five visitors
        // race reserve-random; the post-insert backstop guarantees the session never
        // holds MORE than the declared cap. It may over-correct to fewer under true
        // parallelism, so assert <= cap (never == cap), and that every loser is a
        // 409 SeatSessionFull.
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
        Assert.True(success <= cap, $"expected at most {cap} successes, got {success}");
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
        Assert.True(active <= cap, $"active {active} exceeded cap {cap}");
        Assert.Equal(success, active);
    }

    // -- M-1: approval-time capacity re-check + open-seating join backstop -----

    [Fact]
    public async Task Approving_a_booking_beyond_capacity_is_blocked()
    {
        // M-1 — two Pending open-seating holds slipped past the join pre-check
        // (simulated by a direct insert). Cap is 1, so only the first may be approved;
        // approving the second is a 409 and the booking stays Pending.
        var session = await SeedOpenSeatingSessionAsync(capacity: 1);
        var firstId = await SeedPendingOpenSeatingReservationAsync(session.Id);
        var secondId = await SeedPendingOpenSeatingReservationAsync(session.Id);
        var admin = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
        await svc.ApproveBookingAsync(admin, firstId);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.ApproveBookingAsync(admin, secondId));
        Assert.Equal(ErrorCodes.SeatSessionFull, ex.Code);

        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var approved = await db.SeatReservations
            .CountAsync(r => r.SessionId == session.Id && r.Status == BookingStatus.Approved);
        Assert.Equal(1, approved);
    }

    [Fact]
    public async Task Bulk_approve_stops_at_capacity()
    {
        // M-1 — the sequential bulk-approve re-checks after each commit, so it fills
        // the cap-1 session with exactly one booking and leaves the other Pending.
        var session = await SeedOpenSeatingSessionAsync(capacity: 1);
        var id1 = await SeedPendingOpenSeatingReservationAsync(session.Id);
        var id2 = await SeedPendingOpenSeatingReservationAsync(session.Id);
        var admin = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
        var approvedCount = await svc.BulkApproveBookingsAsync(admin, new[] { id1, id2 });
        Assert.Equal(1, approvedCount);

        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var approved = await db.SeatReservations
            .CountAsync(r => r.SessionId == session.Id && r.Status == BookingStatus.Approved);
        var pending = await db.SeatReservations
            .CountAsync(r => r.SessionId == session.Id && r.Status == BookingStatus.Pending);
        Assert.Equal(1, approved);
        Assert.Equal(1, pending);
    }

    [Fact]
    public async Task Open_seating_join_capacity_is_enforced_under_concurrency()
    {
        // M-1 — six visitors race the open-seating join on a cap-2 session; the
        // post-insert backstop keeps the active count at or below the cap.
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
        Assert.True(success <= cap, $"expected at most {cap} successes, got {success}");
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
        Assert.True(active <= cap, $"active {active} exceeded cap {cap}");
        Assert.Equal(success, active);
    }

    // -- R1-#20: a booking cannot be created once the session has started ------

    [Fact]
    public async Task Reserving_a_seat_after_the_session_started_is_rejected()
    {
        // R1-#20 (FDS-005 §5.1, FR-504) — a seat pick on an already-started session
        // is a 409 BOOKING_SESSION_STARTED, mirroring the cancel-after-start guard,
        // so a visitor can never open a hold they could then never cancel.
        var (session, _) = await SeedSessionWithLayoutAsync(
            new[] { "A" }, seatsPerRow: 3,
            startUtc: DateTimeOffset.UtcNow.AddHours(-1),
            endUtc: DateTimeOffset.UtcNow.AddHours(1));
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
        var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionStarted, body.Error!.Code);
    }

    [Fact]
    public async Task Reserve_random_after_the_session_started_is_rejected()
    {
        // R1-#20 — the random-allocator create path carries the same guard.
        var (session, _) = await SeedSessionWithLayoutAsync(
            new[] { "A" }, seatsPerRow: 3,
            startUtc: DateTimeOffset.UtcNow.AddHours(-1),
            endUtc: DateTimeOffset.UtcNow.AddHours(1));
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve-random", new { }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
        var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionStarted, body.Error!.Code);
    }

    [Fact]
    public async Task Joining_open_seating_after_the_session_started_is_rejected()
    {
        // R1-#20 — the open-seating join create path carries the same guard.
        var session = await SeedOpenSeatingSessionAsync(
            capacity: 50,
            startUtc: DateTimeOffset.UtcNow.AddHours(-1),
            endUtc: DateTimeOffset.UtcNow.AddHours(1));
        var visitor = await SignInApprovedVisitorAsync();

        var join = await PostAuthAsync<object>(
            $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
        var body = (await join.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionStarted, body.Error!.Code);
    }

    // -- R1-#21: the capacity backstop rejects only the true overflow ----------

    [Fact]
    public async Task Open_seating_join_never_rejects_every_racer_for_a_free_place()
    {
        // R1-#21 — the post-insert backstop must reject only the TRUE overflow, not
        // both racers. With one free place and three racers, the earliest hold (by
        // commit order) always survives, so the place is never left unfilled — the
        // spurious "reject BOTH" over-correction the fix removes. The hard upper
        // bound stays covered by Open_seating_join_capacity_is_enforced_under_concurrency.
        const int cap = 1;
        var session = await SeedOpenSeatingSessionAsync(capacity: cap);
        var visitors = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            visitors.Add(await SignInApprovedVisitorAsync());
        }

        var responses = await Task.WhenAll(visitors.Select(v =>
            PostAuthAsync<object>(
                $"/api/v1/app/sessions/{session.Id}/seats/join", new { }, v)));

        var success = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(success >= cap, $"the free place was left unfilled (success {success})");
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

    // -- M-6: a Pending hold is stamped with an expiry -------------------------

    [Fact]
    public async Task Reserving_stamps_an_expiry_on_the_hold()
    {
        // M-6 — a visitor seat pick is stamped with ExpiresUtc = CreatedAt + the
        // hold window; an admin-reserved row seat never expires (ExpiresUtc null).
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
        Assert.NotNull(mine.ExpiresUtc);
        var window = mine.ExpiresUtc!.Value - mine.CreatedAt;
        Assert.True(
            (window - SeatReservationService.PendingHoldWindow).Duration() < TimeSpan.FromSeconds(1),
            $"expiry window {window} not ~ {SeatReservationService.PendingHoldWindow}");

        var adminSeat = await db.SeatReservations
            .Where(r => r.SessionId == session.Id
                && r.Kind == SeatReservationKind.AdminReservedRow)
            .FirstAsync();
        Assert.Null(adminSeat.ExpiresUtc);
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

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedPendingOpenSeatingReservationAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = null,
            SeatNumber = null,
            Kind = SeatReservationKind.OpenSeating,
            ReservedForUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BookingStatus.Pending,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24),
        };
        db.SeatReservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<(Session Session, Hall Hall)> SeedSessionWithLayoutAsync(
        string[] rowLabels, int seatsPerRow,
        SeatSelectionMode hallMode = SeatSelectionMode.AssignedSeat,
        SeatSelectionMode? sessionModeOverride = null,
        int? capacityOverride = null,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null)
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
            // P2.2 — D-227: a FUTURE window by default so bookings can be cancelled
            // before the session starts (the cancel-before-start guard, FR-504).
            // Overridable so a started-session case can be seeded (R1-#20).
            StartUtc = startUtc ?? DateTimeOffset.UtcNow.AddHours(1),
            EndUtc = endUtc ?? DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
    }

    private async Task<Session> SeedOpenSeatingSessionAsync(
        int capacity,
        DateTimeOffset? startUtc = null,
        DateTimeOffset? endUtc = null)
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
            // FUTURE window by default; overridable so a started-session case can be
            // seeded (R1-#20).
            StartUtc = startUtc ?? DateTimeOffset.UtcNow.AddHours(1),
            EndUtc = endUtc ?? DateTimeOffset.UtcNow.AddHours(2),
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
            StartUtc = DateTimeOffset.UtcNow.AddHours(1),
            EndUtc = DateTimeOffset.UtcNow.AddHours(2),
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
