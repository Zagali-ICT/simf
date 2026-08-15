// Journey BF-05 (docs/tests/SIMF-Business-Flows.md § BF-05, E2E-BF-05-001) — one
// seat travelling from booking to recorded attendance, in one run.
//
// Both halves already had tests, separately: SeatReservationsTests drives the
// reserve leg, GateScanTests / GateHallDoorChainTests drive the scan leg. Neither
// could assert the HAND-OFF between them, because their fixtures build two
// different people — the reserving visitor has an app token and no badge, the
// scanned visitor has a badge and no token. So the sentence the flow doc actually
// promises ("the gate check-in confirms the provisionally-held seat") had never
// been executed: nothing proved that the badge the door accepts belongs to the
// account that holds the seat.
//
// The join under test is a chain of ids, all of them on the App DB now that the
// attendee record — not the account — is what every one of them names:
//
//   SeatReservation.ReservedForProfileId
//     == the UserProfile.Id that UserProfile.QrId resolves to (QrResolver)
//     == HallAttendance.UserProfileId
//
// and it is that chain — not any status code — which makes SessionSeatCell
// .CheckedIn flip for THAT reservation and no other. Every assertion below is on
// a value handed from one step to the next: the reservation id returned by the
// reserve call, the QR id printed on the badge, the user id the gate resolved.
//
// A second visitor books a seat in the same session and never scans. They are the
// control: if the confirmation were computed per-session rather than per-holder,
// their seat would light up too and the journey would be passing for the wrong
// reason.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class Journey05BookingToArrivalTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public Journey05BookingToArrivalTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_seat_booked_before_the_session_is_confirmed_when_its_holder_scans_in_at_the_hall_door()
    {
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A", "B" }, seatsPerRow: 5);
        var holder = await CreateApprovedVisitorWithBadgeAsync("Faisal Al-Otaibi", "فيصل العتيبي");
        var control = await CreateApprovedVisitorWithBadgeAsync("Noura Al-Harbi", "نورة الحربي");
        var (operatorToken, operatorUserId) = await CreateAdministratorAndSignInAsync();

        // -- Step 1: the visitor picks seat A3 from the grid the app rendered.
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 3 }, holder.Token);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var booking = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(BookingStatus.Approved, booking.Status);   // auto-confirmed on create (D-227)
        Assert.Equal(SeatReservationKind.UserBooking, booking.Kind);
        Assert.Equal("A", booking.RowLabel);
        Assert.Equal(3, booking.SeatNumber);

        var controlPick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "B", SeatNumber = 4 }, control.Token);
        Assert.Equal(HttpStatusCode.OK, controlPick.StatusCode);
        var controlBooking = (await controlPick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;

        // The stored hold is keyed to the attendee PROFILE — the same id the badge
        // QR will have to resolve to three steps from now, or the chain is broken.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var stored = await db.SeatReservations.AsNoTracking()
                .SingleAsync(r => r.Id == booking.ReservationId);
            Assert.Equal(holder.ProfileId, stored.ReservedForProfileId);
            Assert.Null(stored.ReleasedAt);
        }

        // -- Step 2: until someone arrives the seat is held, not confirmed. This is
        // the baseline the flip is measured against — without it a CheckedIn that
        // was true all along would read as a successful journey.
        var beforeArrival = await SeatMapAsync(session.Id, holder.Token);
        Assert.Equal(booking.ReservationId, beforeArrival.MyCell!.ReservationId);
        Assert.False(beforeArrival.MyCell.CheckedIn);
        Assert.Equal(2, beforeArrival.ActiveReservedCount);

        // -- Step 3: the door operator scans the badge issued to THAT account.
        var gateId = await CreateHallDoorGateAsync(operatorToken, operatorUserId, hall.Id);
        var scan = await PostScanAsync(gateId, holder.QrId, operatorToken, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
        var scanned = (await scan.Content
            .ReadFromJsonAsync<ApiResult<GateScanResponse>>())!.Data!;
        Assert.Equal(ScanOutcome.Allowed, scanned.Outcome);
        Assert.Equal(ScanDirection.CheckIn, scanned.Direction);
        // The gate resolved the badge server-side to the holder's profile — the
        // request carried a QR string and nothing else.
        Assert.Equal(holder.ProfileId, scanned.UserProfile!.Id);
        // DEF-CHK-004 — an advisory notice here would mean the scan admitted the
        // holder but recorded NO attendance, which is precisely the silent failure
        // this journey exists to rule out.
        Assert.Null(scanned.NoticeMessage);

        // -- Step 4: exactly one attendance row, bound to the reservation's holder.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var attendance = Assert.Single(await db.HallAttendances.AsNoTracking()
                .Where(a => a.SessionId == session.Id)
                .ToListAsync());
            Assert.Equal(holder.ProfileId, attendance.UserProfileId); // == ReservedForProfileId
            Assert.Equal(hall.Id, attendance.HallId);
            Assert.Equal(AttendanceMethod.QrScan, attendance.Method);
            Assert.Null(attendance.Leave);

            // And the gate row that produced it names the same badge + profile, so
            // the audit trail and the attendance agree on who walked in.
            var gateScan = await db.GateScans.AsNoTracking()
                .SingleAsync(s => s.GateId == gateId);
            Assert.Equal(ScanOutcome.Allowed, gateScan.Outcome);
            Assert.Equal(holder.ProfileId, gateScan.UserProfileId);
            Assert.Equal(holder.QrId, gateScan.QrIdAtScan);
        }

        // -- Step 5: the visitor's own seat card now reads confirmed…
        var afterArrival = await SeatMapAsync(session.Id, holder.Token);
        Assert.Equal(booking.ReservationId, afterArrival.MyCell!.ReservationId);
        Assert.True(afterArrival.MyCell.CheckedIn);
        Assert.Equal(BookingStatus.Approved, afterArrival.MyCell.Status);
        // …and the visitor who never scanned is untouched, which is what makes the
        // flip attributable to the badge rather than to the arrival of anybody.
        var controlCell = afterArrival.ReservedCells
            .Single(c => c.ReservationId == controlBooking.ReservationId);
        Assert.False(controlCell.CheckedIn);

        // -- Step 6: the same arrival is what the Control Panel seat plan shows the
        // operator, named and attributed — the surface a release decision is made on.
        var plan = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/seats/list",
            new GridQuery { Top = 50 }, operatorToken);
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        var page = (await plan.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SeatPlanCell>>>())!.Data!;
        var holderCell = page.Items.Single(c => c.ReservationId == booking.ReservationId);
        Assert.Equal(holder.ProfileId, holderCell.HolderProfileId);
        Assert.Equal("Faisal Al-Otaibi", holderCell.HolderName);
        Assert.True(holderCell.CheckedIn);
        Assert.False(page.Items
            .Single(c => c.ReservationId == controlBooking.ReservationId).CheckedIn);
    }

    [Fact]
    public async Task The_hall_arrivals_console_confirms_the_same_held_seat_as_the_turnstile()
    {
        // BF-05 names the operator console (POST /admin/sessions/{id}/arrivals) as
        // the arrival OF RECORD, while the turnstile above reaches attendance
        // through the gate-scan chain. They are two doors onto the same join, so
        // the branch is worth its own run: a console scan must confirm the held
        // seat exactly as the gate does, resolving the attendee from the QR alone.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var holder = await CreateApprovedVisitorWithBadgeAsync("Saud Al-Qahtani", "سعود القحطاني");
        var (operatorToken, _) = await CreateAdministratorAndSignInAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, holder.Token);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var booking = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;

        var arrival = await PostAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/arrivals",
            new RecordQrArrivalRequest { QrId = holder.QrId }, operatorToken);
        Assert.Equal(HttpStatusCode.OK, arrival.StatusCode);
        var recorded = (await arrival.Content
            .ReadFromJsonAsync<ApiResult<QrArrivalResult>>())!.Data!;
        // The console echoes the attendee it derived from the badge; that it equals
        // the seat's holder is the whole point of the scan.
        Assert.Equal(holder.UserId, recorded.UserId);
        Assert.True(recorded.Status.Arrived);
        Assert.Equal(AttendanceMethod.QrScan, recorded.Status.Method);

        var map = await SeatMapAsync(session.Id, holder.Token);
        Assert.Equal(booking.ReservationId, map.MyCell!.ReservationId);
        Assert.True(map.MyCell.CheckedIn);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = Assert.Single(await db.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == session.Id)
            .ToListAsync());
        Assert.Equal(holder.ProfileId, row.UserProfileId);
        // D-227 / D-244 — arrival never writes to the booking: the reservation is
        // still the same live Approved hold, only its view of attendance changed.
        var stored = await db.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == booking.ReservationId);
        Assert.Equal(BookingStatus.Approved, stored.Status);
        Assert.Null(stored.ReleasedAt);
        Assert.Null(stored.ReviewedByUserId);
    }

    [Fact]
    public async Task Scanning_back_out_clears_the_confirmation_while_the_seat_stays_held()
    {
        // The confirmed state is a LIVE read of the open attendance row, not a latch
        // the arrival sets once. Walking back out through the same door must return
        // the seat to "reserved" — and must not disturb the reservation itself,
        // which the attendee still holds for the rest of the session.
        var (session, hall) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 5);
        var holder = await CreateApprovedVisitorWithBadgeAsync("Reem Al-Dossari", "ريم الدوسري");
        var (operatorToken, operatorUserId) = await CreateAdministratorAndSignInAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 2 }, holder.Token);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var booking = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;

        var gateId = await CreateHallDoorGateAsync(operatorToken, operatorUserId, hall.Id);
        var checkIn = await PostScanAsync(gateId, holder.QrId, operatorToken, ScanDirection.CheckIn);
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        Assert.True((await SeatMapAsync(session.Id, holder.Token)).MyCell!.CheckedIn);

        // D-509 — a deliberate direction switch on a Both-mode gate is a real second
        // movement, so it is not swallowed by the 5-second duplicate window.
        var checkOut = await PostScanAsync(gateId, holder.QrId, operatorToken, ScanDirection.CheckOut);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);

        var afterDeparture = await SeatMapAsync(session.Id, holder.Token);
        Assert.Equal(booking.ReservationId, afterDeparture.MyCell!.ReservationId);
        Assert.False(afterDeparture.MyCell.CheckedIn);
        Assert.Equal(BookingStatus.Approved, afterDeparture.MyCell.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var attendance = Assert.Single(await db.HallAttendances.AsNoTracking()
            .Where(a => a.SessionId == session.Id)
            .ToListAsync());
        Assert.Equal(holder.ProfileId, attendance.UserProfileId);
        Assert.NotNull(attendance.Leave);
        var stored = await db.SeatReservations.AsNoTracking()
            .SingleAsync(r => r.Id == booking.ReservationId);
        Assert.Null(stored.ReleasedAt);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>One person who can do BOTH halves of the journey. The per-leg
    /// fixtures each build half of them: <c>SeatReservationsTests</c> signs a
    /// visitor up through the API (an app token, no badge — sign-up writes no
    /// <c>UserProfile</c>), while <c>GateScanTests</c> seeds a profile carrying a
    /// <c>QrId</c> (a badge, no token). The journey needs the same account to hold
    /// the seat and to be recognised at the door.</summary>
    private sealed record Attendee(Guid UserId, Guid ProfileId, string QrId, string Token);

    /// <summary>Modelled on <c>HallArrivalScanTests.CreateApprovedVisitorWithQrAsync</c>
    /// — an Approved visitor with a badge QR who then signs in for an app token.
    /// The account is created Approved BEFORE sign-in so the minted JWT carries
    /// <c>account_state=Approved</c> for the seat endpoints' RequireApprovedAccount
    /// policy (the claim is stamped at sign-in, never re-read live).</summary>
    private async Task<Attendee> CreateApprovedVisitorWithBadgeAsync(
        string name, string nameArabic)
    {
        var email = $"journey05-visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var profileId = Guid.NewGuid();
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = name,
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = profileId,
                UserId = user.Id,
                QrId = qrId,
                Name = name,
                NameArabic = nameArabic,
                NationalityId = 682,   // ISO 3166-1 numeric — SA
                PlaceOfBirth = "Riyadh",
                // The hall door reads admission off the PROFILE; left at its
                // PendingApproval default the holder is refused before the seat
                // confirmation this journey exists to prove.
                AdmissionState = AccountState.Approved,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return new Attendee(userId, profileId, qrId, body.Data!.Tokens!.AccessToken);
    }

    /// <summary>The same layout seeding <c>SeatReservationsTests</c> uses, with one
    /// deliberate change: the session starts in 10 minutes rather than an hour.
    /// That is the only window in which the whole journey is legal — the booking
    /// paths accept any session that has not ENDED, but the door chain only binds
    /// an arrival to a session inside the ±15-minute arrival grace. A seat booked
    /// against a session an hour out can never be checked in, so a journey seeded
    /// that way would silently test nothing.</summary>
    private async Task<(Session Session, Hall Hall)> SeedSessionWithLayoutAsync(
        string[] rowLabels, int seatsPerRow)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Journey Hall", NameArabic = "قاعة الرحلة",
            Capacity = rowLabels.Length * seatsPerRow,
            SeatSelectionMode = SeatSelectionMode.AssignedSeat,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        // No SeatTiers — a pre-D-771 layout reads Normal for every row, so an
        // ordinary (non-VIP) visitor may book any seat in it.
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = string.Join(',', rowLabels),
            SeatsPerRow = seatsPerRow,
            CreatedAt = SimfClock.Now,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Maritime Cyber Defence", TitleArabic = "الدفاع السيبراني البحري",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(10),
            End = SimfClock.Now.AddMinutes(70),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
    }

    /// <summary>A gate bound to the hall (<c>HallId</c> set) — the binding is what
    /// makes a scan feed hall attendance at all; a perimeter gate records only a
    /// GateScan row. The operator is assigned to it, or their own scan is a 403.</summary>
    private async Task<Guid> CreateHallDoorGateAsync(
        string operatorToken, Guid operatorUserId, Guid hallId)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"J5-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Name = "Journey Hall Door",
                NameArabic = "باب قاعة الرحلة",
                DirectionMode = DirectionMode.Both,
                HallId = hallId,
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = new List<Guid> { operatorUserId },
            },
            operatorToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
        Assert.Equal(hallId, detail.HallId);
        return detail.Id;
    }

    private Task<HttpResponseMessage> PostScanAsync(
        Guid gateId, string qr, string operatorToken, ScanDirection direction)
    {
        // The endpoint binds its own PostScanRequest ("direction"), not the
        // service-layer GateScanRequest ("requestedDirection") — post the shape the
        // route actually reads, as the gate tests do.
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new
            {
                qr,
                idempotencyKey = (string?)null,
                source = ScanSource.Simulator,
                direction,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        return _client.SendAsync(request);
    }

    private async Task<SessionSeatMap> SeatMapAsync(Guid sessionId, string visitorToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/app/sessions/{sessionId}/seats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", visitorToken);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
    }

    private async Task<(string Token, Guid UserId)> CreateAdministratorAndSignInAsync()
    {
        var email = $"journey05-operator-{Guid.NewGuid():N}@simf.test";
        Guid userId;
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
                DisplayName = "Journey Door Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            userId = user.Id;
        }
        return (await AuthFlow.SignInControlPanelAsync(_client, _factory, email), userId);
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
}
