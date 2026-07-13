// P2.2 — D-227 (SIMF-FDS-005 §5): booking approval workflow over the D-175
// seat-reservation surface. Reserve creates a Pending, held booking; the CP
// approves (seat confirmed + booking-confirmed notification) or rejects (seat
// released + reason recorded + attendee notified). No-overlap (FR-502) and
// cancel-before-start (FR-504) are enforced.
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

public sealed class BookingApprovalTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BookingApprovalTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Approve_confirms_the_seat_and_writes_booking_confirmed()
    {
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
        var visitor = await SignInApprovedVisitorAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var reservationId = await ReserveAsync(session.Id, "A", 1, visitor);

        // The Pending booking shows in the approval queue.
        var queue = await ListQueueAsync(admin);
        Assert.Contains(queue.Items, r => r.ReservationId == reservationId);

        var approve = await PostAuthAsync(
            $"/api/v1/admin/bookings/{reservationId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // Booking-confirmed notification now exists (it did NOT at reserve time).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var note = await db.Notifications.SingleOrDefaultAsync(
            n => n.Kind == NotificationKind.BookingConfirmed
                && n.RelatedEntityId == session.Id);
        Assert.NotNull(note);

        // …and it is gone from the queue.
        var after = await ListQueueAsync(admin);
        Assert.DoesNotContain(after.Items, r => r.ReservationId == reservationId);
    }

    [Fact]
    public async Task Reject_with_a_reason_releases_the_seat_and_notifies()
    {
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
        var visitor = await SignInApprovedVisitorAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var reservationId = await ReserveAsync(session.Id, "A", 1, visitor);

        var reject = await PostAuthAsync(
            $"/api/v1/admin/bookings/{reservationId}/reject",
            new RejectBookingRequest { Reason = "Seat reserved for VIP delegation." },
            admin);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        // The held seat is released — the same seat can be re-booked.
        var rebook = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, rebook.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var note = await db.Notifications.SingleOrDefaultAsync(
            n => n.Kind == NotificationKind.BookingRejected
                && n.RelatedEntityId == session.Id);
        Assert.NotNull(note);
        Assert.Contains("VIP", note!.Body);
    }

    [Fact]
    public async Task Reject_without_a_reason_is_400()
    {
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
        var visitor = await SignInApprovedVisitorAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var reservationId = await ReserveAsync(session.Id, "A", 1, visitor);

        var reject = await PostAuthAsync(
            $"/api/v1/admin/bookings/{reservationId}/reject",
            new RejectBookingRequest { Reason = "   " }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, reject.StatusCode);
        var body = (await reject.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingRejectionReasonRequired, body.Error!.Code);
    }

    [Fact]
    public async Task Overlapping_booking_in_another_session_is_blocked()
    {
        // Two sessions, different halls, overlapping windows.
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var end = start.AddHours(1);
        var session1 = await SeedSessionAsync(start, end);
        var session2 = await SeedSessionAsync(start.AddMinutes(30), end.AddMinutes(30));
        var visitor = await SignInApprovedVisitorAsync();

        await ReserveAsync(session1.Id, "A", 1, visitor);

        var clash = await PostAuthAsync(
            $"/api/v1/app/sessions/{session2.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingOverlap, body.Error!.Code);
    }

    [Fact]
    public async Task Cancel_after_the_session_has_started_is_refused()
    {
        // Seed an already-started session and reserve directly (bypassing the
        // app start-guard, which only applies to cancellation, not booking).
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        var visitor = await SignInApprovedVisitorAsync();
        await ReserveAsync(session.Id, "A", 1, visitor);

        var cancel = await DeleteAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/mine", visitor);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        var body = (await cancel.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionStarted, body.Error!.Code);
    }

    [Fact]
    public async Task Booking_a_live_in_progress_session_is_allowed()
    {
        // #20 (Round-1 held, option C) — a walk-in may still book a session that has
        // STARTED but not yet ended (live, in-progress); only an ENDED session is blocked.
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
    }

    [Fact]
    public async Task Booking_an_ended_session_is_refused()
    {
        // #20 (Round-1 held, option C) — an ENDED session can no longer be booked: the
        // hold could never be attended, so the create paths return BOOKING_SESSION_ENDED.
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1));
        var visitor = await SignInApprovedVisitorAsync();

        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/seats/reserve",
            new ReserveSeatRequest { RowLabel = "A", SeatNumber = 1 }, visitor);
        Assert.Equal(HttpStatusCode.Conflict, pick.StatusCode);
        var body = (await pick.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BookingSessionEnded, body.Error!.Code);
    }

    [Fact]
    public async Task Bulk_approve_approves_the_selected_bookings()
    {
        var session = await SeedSessionAsync(
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2));
        var v1 = await SignInApprovedVisitorAsync();
        var v2 = await SignInApprovedVisitorAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var r1 = await ReserveAsync(session.Id, "A", 1, v1);
        var r2 = await ReserveAsync(session.Id, "A", 2, v2);

        var bulk = await PostAuthAsync(
            "/api/v1/admin/bookings/bulk-approve",
            new { ReservationIds = new[] { r1, r2 } }, admin);
        Assert.Equal(HttpStatusCode.OK, bulk.StatusCode);
        var approved = (await bulk.Content.ReadFromJsonAsync<ApiResult<int>>())!.Data;
        Assert.Equal(2, approved);

        var after = await ListQueueAsync(admin);
        Assert.DoesNotContain(after.Items, r => r.ReservationId == r1 || r.ReservationId == r2);
    }

    [Fact]
    public async Task Non_admin_cannot_view_the_booking_queue()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/bookings/list", new GridQuery { Top = 50 }, visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> ReserveAsync(Guid sessionId, string row, int seat, string token)
    {
        var pick = await PostAuthAsync(
            $"/api/v1/app/sessions/{sessionId}/seats/reserve",
            new ReserveSeatRequest { RowLabel = row, SeatNumber = seat }, token);
        Assert.Equal(HttpStatusCode.OK, pick.StatusCode);
        var mine = (await pick.Content
            .ReadFromJsonAsync<ApiResult<MySeatReservation>>())!.Data!;
        Assert.Equal(BookingStatus.Pending, mine.Status);
        return mine.ReservationId;
    }

    private async Task<GridPage<BookingQueueRow>> ListQueueAsync(string adminToken)
    {
        var list = await PostAuthAsync(
            "/api/v1/admin/bookings/list", new GridQuery { Top = 200 }, adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        return (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<BookingQueueRow>>>())!.Data!;
    }

    private async Task<Session> SeedSessionAsync(DateTimeOffset start, DateTimeOffset end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 10, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = "A,B",
            SeatsPerRow = 5,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Booking session", TitleArabic = "جلسة الحجز",
            HallId = hall.Id,
            StartUtc = start,
            EndUtc = end,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"bk-visitor-{Guid.NewGuid():N}@simf.test";
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
        var email = $"bk-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "BK Admin",
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
