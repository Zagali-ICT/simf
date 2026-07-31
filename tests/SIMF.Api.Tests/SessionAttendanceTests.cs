// FR-506 (SIMF-SRS-001 §3.5; FDS-003 §5.5) — the admin session-attendance
// dashboard: per-session distinct-attendee + live-now counts (POST
// /admin/attendance/sessions/list) and the live top-line (GET
// /admin/attendance/summary). Read-only aggregate over HallAttendance (D-241);
// no schema. Attendees are opaque Guids (D-157) so no Identity users are seeded.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionAttendanceTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionAttendanceTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_returns_distinct_attendee_and_live_now_per_session()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // 3 distinct attendees: A (a closed + an open row — re-entry), B (open),
        // C (closed). Distinct attendees = 3; live-now (open rows) = 2 (A, B).
        var (sessionId, code) = await SeedSessionWithAttendanceAsync(openCount: 2, closedOnlyCount: 1, reentrant: true);

        var list = await PostAuthAsync("/api/v1/admin/attendance/sessions/list",
            new GridQuery { Top = 200, Filters = new() { ["code"] = code } }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SessionAttendanceRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.SessionId == sessionId);
        Assert.Equal(3, row.TotalAttendees);
        Assert.Equal(2, row.LiveNow);
        Assert.Equal("Attendance Hall", row.HallName);
    }

    [Fact]
    public async Task List_empty_session_has_zero_counts()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (sessionId, code) = await SeedSessionWithAttendanceAsync(openCount: 0, closedOnlyCount: 0, reentrant: false);

        var list = await PostAuthAsync("/api/v1/admin/attendance/sessions/list",
            new GridQuery { Top = 200, Filters = new() { ["code"] = code } }, admin);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SessionAttendanceRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.SessionId == sessionId);
        Assert.Equal(0, row.TotalAttendees);
        Assert.Equal(0, row.LiveNow);
    }

    [Fact]
    public async Task Summary_lower_bounds_reflect_seeded_arrivals()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // 2 open + 1 closed = 3 distinct attendees, 2 currently inside.
        await SeedSessionWithAttendanceAsync(openCount: 2, closedOnlyCount: 1, reentrant: false);

        var get = await GetAuthAsync("/api/v1/admin/attendance/summary", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var summary = (await get.Content
            .ReadFromJsonAsync<ApiResult<SessionAttendanceSummary>>())!.Data!;

        // The shared test DB carries rows from other classes too, so assert
        // stable lower bounds: my 2 open attendees and 3 arrivals are a subset
        // of the global aggregate, and my session has attendance.
        Assert.True(summary.LiveAttendeesNow >= 2);
        Assert.True(summary.TotalArrivals >= 3);
        Assert.True(summary.SessionsWithAttendance >= 1);
    }

    [Fact]
    public async Task Summary_is_forbidden_for_a_non_admin()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/admin/attendance/summary", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_is_forbidden_for_a_non_admin()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync("/api/v1/admin/attendance/sessions/list",
            new GridQuery { Top = 50 }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- 2026-07-18: live per-session hall view (present list + 4-state seat map) --

    [Fact]
    public async Task Present_lists_currently_inside_attendees_with_profile_and_seat()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (sessionId, hallId) = await SeedSessionWithLayoutAsync();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(userId, "Faisal Al-Harbi", "فيصل الحربي", "Captain");
        await SeedOpenAttendanceAsync(sessionId, hallId, userId);
        await SeedSeatAsync(sessionId, userId, "A", 3);

        var response = await GetAuthAsync($"/api/v1/admin/sessions/{sessionId}/present", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var present = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionPresentAttendee>>>())!.Data!;
        var attendee = Assert.Single(present);
        Assert.Equal(userId, attendee.UserId);
        Assert.Equal("Faisal Al-Harbi", attendee.Name);
        Assert.Equal("فيصل الحربي", attendee.NameArabic);
        Assert.Equal("Captain", attendee.JobTitle);
        Assert.Equal("A", attendee.RowLabel);
        Assert.Equal(3, attendee.SeatNumber);
    }

    [Fact]
    public async Task Present_excludes_a_departed_attendee()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (sessionId, hallId) = await SeedSessionWithLayoutAsync();
        var here = Guid.NewGuid();
        var gone = Guid.NewGuid();
        await SeedProfileAsync(here, "Present One", "الحاضر", null);
        await SeedProfileAsync(gone, "Gone Two", "المغادر", null);
        await SeedOpenAttendanceAsync(sessionId, hallId, here);
        await SeedClosedAttendanceAsync(sessionId, hallId, gone);

        var response = await GetAuthAsync($"/api/v1/admin/sessions/{sessionId}/present", admin);
        var list = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SessionPresentAttendee>>>())!.Data!;
        Assert.Single(list);
        Assert.Equal(here, list[0].UserId);
    }

    [Fact]
    public async Task Present_is_forbidden_for_a_non_admin()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var (sessionId, _) = await SeedSessionWithLayoutAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/sessions/{sessionId}/present", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_seat_map_marks_a_checked_in_reservation_confirmed()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var (sessionId, hallId) = await SeedSessionWithLayoutAsync();
        var userId = Guid.NewGuid();
        await SeedSeatAsync(sessionId, userId, "A", 1);            // reserved
        await SeedOpenAttendanceAsync(sessionId, hallId, userId);  // checked in → confirmed

        var response = await GetAuthAsync($"/api/v1/admin/sessions/{sessionId}/seat-map", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = (await response.Content
            .ReadFromJsonAsync<ApiResult<SessionSeatMap>>())!.Data!;
        Assert.Null(map.MyCell); // admin view has no "my seat"
        var cell = Assert.Single(map.ReservedCells, c => c.RowLabel == "A" && c.SeatNumber == 1);
        Assert.True(cell.CheckedIn); // reserved + open attendance row = confirmed
    }

    [Fact]
    public async Task Admin_seat_map_is_forbidden_for_a_non_admin()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var (sessionId, _) = await SeedSessionWithLayoutAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/sessions/{sessionId}/seat-map", visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- helpers --

    /// <summary>Seeds one active session in its own hall, plus HallAttendance
    /// rows: <paramref name="openCount"/> attendees currently inside (open row),
    /// <paramref name="closedOnlyCount"/> attendees who already left (closed
    /// row), and — when <paramref name="reentrant"/> — one extra CLOSED row for
    /// the first open attendee (a re-entry) so the distinct-attendee count must
    /// dedupe it. Returns the session id and its unique code.</summary>
    private async Task<(Guid SessionId, string Code)> SeedSessionWithAttendanceAsync(
        int openCount, int closedOnlyCount, bool reentrant)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var now = SimfClock.Now;
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "HA-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Attendance Hall", NameArabic = "قاعة الحضور",
            Capacity = 200, IsActive = true, CreatedAt = now,
        };
        db.Halls.Add(hall);

        var code = "ATT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = "Attendance Dashboard Session", TitleArabic = "جلسة لوحة الحضور",
            HallId = hall.Id,
            Start = now.AddMinutes(-15),
            End = now.AddMinutes(45),
            IsActive = true, CreatedAt = now,
        };
        db.Sessions.Add(session);

        for (var i = 0; i < openCount; i++)
        {
            var userId = Guid.NewGuid();
            if (reentrant && i == 0)
            {
                // An earlier visit that already closed, then a fresh open row.
                db.HallAttendances.Add(NewAttendance(session.Id, hall.Id, userId,
                    now.AddMinutes(-12), now.AddMinutes(-8)));
            }
            db.HallAttendances.Add(NewAttendance(session.Id, hall.Id, userId,
                now.AddMinutes(-5), leave: null));
        }

        for (var i = 0; i < closedOnlyCount; i++)
        {
            db.HallAttendances.Add(NewAttendance(session.Id, hall.Id, Guid.NewGuid(),
                now.AddMinutes(-10), now.AddMinutes(-2)));
        }

        await db.SaveChangesAsync();
        return (session.Id, code);
    }

    private static HallAttendance NewAttendance(
        Guid sessionId, Guid hallId, Guid userId,
        DateTime enter, DateTime? leave) =>
        new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserId = userId,
            Method = AttendanceMethod.Geofence,
            Enter = enter,
            Leave = leave,
            CreatedAt = SimfClock.Now,
        };

    private async Task<(Guid SessionId, Guid HallId)> SeedSessionWithLayoutAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "HL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Live Hall", NameArabic = "قاعة مباشرة",
            Capacity = 100, IsActive = true, CreatedAt = now,
        };
        db.Halls.Add(hall);
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = "A,B",
            SeatsPerRow = 5,
            CreatedAt = now,
        });
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "LIVE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Title = "Live Hall Session", TitleArabic = "جلسة القاعة المباشرة",
            HallId = hall.Id,
            Start = now.AddMinutes(-15),
            End = now.AddMinutes(45),
            IsActive = true, CreatedAt = now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session.Id, hall.Id);
    }

    private async Task SeedProfileAsync(Guid userId, string name, string nameArabic, string? jobTitle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            NameArabic = nameArabic,
            JobTitle = jobTitle,
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedOpenAttendanceAsync(Guid sessionId, Guid hallId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.HallAttendances.Add(NewAttendance(sessionId, hallId, userId,
            SimfClock.Now.AddMinutes(-3), leave: null));
        await db.SaveChangesAsync();
    }

    private async Task SeedClosedAttendanceAsync(Guid sessionId, Guid hallId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.HallAttendances.Add(NewAttendance(sessionId, hallId, userId,
            SimfClock.Now.AddMinutes(-10), SimfClock.Now.AddMinutes(-2)));
        await db.SaveChangesAsync();
    }

    private async Task SeedSeatAsync(Guid sessionId, Guid userId, string row, int seat)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = SeatReservationKind.UserBooking,
            ReservedForUserId = userId,
            CreatedByUserId = userId,
            CreatedAt = SimfClock.Now,
            Status = BookingStatus.Approved,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-attendance-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Admin Attendance",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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
