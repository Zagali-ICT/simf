// D-175 (gap doc G11, Mockup page 7) — per-session seat reservations.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
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
            $"/api/v1/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 3 },
            visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal("A", mine.RowLabel);
        Assert.Equal(3, mine.SeatNumber);
        Assert.Equal(SeatReservationKind.UserBooking, mine.Kind);

        var release = await DeleteAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/mine", visitor);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        // After release, picking the same seat again should succeed.
        var rePick = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/reserve",
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
            $"/api/v1/sessions/{session.Id}/seats/reserve",
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
            $"/api/v1/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/reserve",
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
            $"/api/v1/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/reserve",
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
            $"/api/v1/sessions/{session.Id}/seats/reserve-random",
            new { }, visitor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = (await response.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(SeatReservationKind.RandomAssignment, mine.Kind);
        Assert.Equal("A", mine.RowLabel);
        Assert.InRange(mine.SeatNumber, 1, 2);
    }

    [Fact]
    public async Task Capacity_cap_blocks_overbooking()
    {
        // Layout 1x1 — session capacity is 1, then the second visitor should fail.
        var (session, _) = await SeedSessionWithLayoutAsync(new[] { "A" }, seatsPerRow: 1);
        var v1 = await SignInApprovedVisitorAsync();
        var v2 = await SignInApprovedVisitorAsync();

        var first = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, v1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync<object>(
            $"/api/v1/sessions/{session.Id}/seats/reserve-random",
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
            $"/api/v1/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, attempt.StatusCode);

        // Row B is still free.
        var freeRow = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/seats/reserve",
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

    // -- Helpers --------------------------------------------------------------

    private async Task<(Session Session, Hall Hall)> SeedSessionWithLayoutAsync(
        string[] rowLabels, int seatsPerRow)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = rowLabels.Length * seatsPerRow,
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
            // P2.2 — D-227: a FUTURE window so bookings can be cancelled before
            // the session starts (the new cancel-before-start guard, FR-504).
            StartUtc = DateTimeOffset.UtcNow.AddHours(1),
            EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session, hall);
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
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
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
            "/api/v1/auth/sign-in",
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
}
