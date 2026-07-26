// Regression tests for the session-lifecycle QA package:
//   A1/A6 — a hall or start/end change silently cancelled every active registration
//           (and destroyed admin row-blocks) with no count, no audit, no warning.
//   A2    — the seat-release notice was in-app only, so a visitor who never opens
//           the app never learned their seat was gone.
//   A4    — a rescheduled session was permanently un-remindable (ReminderSent /
//           RatingPromptSent were never cleared).
//   A5    — the SESSION_HAS_ACTIVE_BOOKINGS message sent the admin to the read-only
//           bookings monitor instead of the page that can release a seat.
//   B2    — cancelling a session told nobody at all.
//   A37   — deactivating a hall ignored the sessions still using it.
// Runs on BulkBadgeEmailApiFactory so the email half is captured synchronously.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionLifecycleNoticeTests : IClassFixture<BulkBadgeEmailApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string SeatPlansRoute = "/admin/sessions/seat-plans";

    private readonly BulkBadgeEmailApiFactory _factory;
    private readonly HttpClient _client;

    public SessionLifecycleNoticeTests(BulkBadgeEmailApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- A1 / A6: the blast radius is visible before AND after the change ------

    [Fact]
    public async Task A1_Get_stamps_the_holding_a_hall_or_time_change_would_release()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var visitor = await SeedApprovedVisitorAsync();
        await SeedReservationAsync(session.Id, visitor.Id, "A", 1);
        await SeedReservationAsync(session.Id, reservedForUserId: null, "B", 1);
        await SeedReservationAsync(session.Id, reservedForUserId: null, "B", 2);

        var detail = await GetSessionAsync(session.Id, token);

        // The CP edit form reads exactly these two numbers to warn before it submits.
        Assert.Equal(1, detail.ActiveReservationCount);
        Assert.Equal(2, detail.ActiveAdminBlockCount);
    }

    [Fact]
    public async Task A1_A6_Hall_change_reports_and_audits_what_it_released()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var origin = await SeedHallAsync(capacity: 50);
        var destination = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, origin.Id);
        var visitor = await SeedApprovedVisitorAsync();
        await SeedReservationAsync(session.Id, visitor.Id, "A", 1);
        await SeedReservationAsync(session.Id, reservedForUserId: null, "B", 1);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, hallId: destination.Id),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        Assert.Equal(1, updated.ReleasedReservationCount);
        // A6 — the admin row-block is destroyed too and had no other trace at all.
        Assert.Equal(1, updated.ReleasedAdminBlockCount);

        var audit = FindAudit(AuditEvents.SeatReservationReleased, $"session={session.Id}");
        Assert.NotNull(audit);
        Assert.Contains("reason=HallChanged", audit!.Detail);
        Assert.Contains("reservations=1", audit.Detail);
        Assert.Contains("adminBlocks=1", audit.Detail);
    }

    [Fact]
    public async Task A1_An_edit_that_leaves_the_slot_alone_reports_no_releases()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var visitor = await SeedApprovedVisitorAsync();
        await SeedReservationAsync(session.Id, visitor.Id, "A", 1);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, title: "Renamed only"),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        Assert.Equal(0, updated.ReleasedReservationCount);
        Assert.Equal(0, updated.ReleasedAdminBlockCount);
        Assert.Null(FindAudit(AuditEvents.SeatReservationReleased, $"session={session.Id}"));
    }

    // -- A2: the release notice reaches a visitor who never opens the app -----

    [Fact]
    public async Task A2_Released_seat_notice_is_emailed_not_only_in_app()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var origin = await SeedHallAsync(capacity: 50);
        var destination = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, origin.Id);
        var visitor = await SeedApprovedVisitorAsync();
        await SeedReservationAsync(session.Id, visitor.Id, "A", 1);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, hallId: destination.Id),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var row = await identity.Notifications.AsNoTracking().SingleAsync(notification =>
            notification.UserId == visitor.Id
            && notification.Kind == NotificationKind.BookingRejected);
        // Local time only — the copy must never leak a UTC timestamp (D-219).
        Assert.Contains(session.Start.FormatSaudi(), row.Body);
        Assert.Contains(session.Start.FormatSaudi(), row.BodyArabic);
        Assert.DoesNotContain("UTC", row.Body);

        Assert.Contains(_factory.Emails.Messages, message => message.To == visitor.Email);
    }

    // -- A4: a rescheduled session is remindable again ------------------------

    [Fact]
    public async Task A4_Moving_the_window_rearms_the_reminder_and_rating_prompt()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        await StampWorkerFlagsAsync(session.Id);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session,
                start: session.Start.AddHours(3),
                end: session.End.AddHours(3)),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await LoadSessionAsync(session.Id);
        // SessionReminderWorker filters on ReminderSent == null; leaving the stamp
        // made a moved session permanently un-remindable.
        Assert.Null(stored.ReminderSent);
        Assert.Null(stored.RatingPromptSent);
    }

    [Fact]
    public async Task A4_An_edit_that_keeps_the_window_does_not_rearm_the_reminder()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        await StampWorkerFlagsAsync(session.Id);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, title: "Renamed only"),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await LoadSessionAsync(session.Id);
        // An unrelated save must not resend an already-delivered reminder.
        Assert.NotNull(stored.ReminderSent);
        Assert.NotNull(stored.RatingPromptSent);
    }

    // -- A5: the conflict names the page that can actually do the work -------

    [Fact]
    public async Task A5_Active_booking_conflict_points_at_the_seat_plans_page()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var visitor = await SeedApprovedVisitorAsync();
        await SeedReservationAsync(session.Id, visitor.Id, "A", 1);

        var delete = await DeleteAuthAsync($"/api/v1/admin/sessions/{session.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionHasActiveBookings, body.Error!.Code);
        // The old copy said "cancel or reject them", which only the read-only
        // /admin/bookings monitor sounded like — and it has no row actions.
        Assert.Contains(SeatPlansRoute, body.Error.Message);
        Assert.Contains(SeatPlansRoute, body.Error.MessageArabic);
        Assert.DoesNotContain("/admin/bookings", body.Error.Message);
    }

    // -- B2: cancelling a session finally tells somebody ----------------------

    [Fact]
    public async Task B2_Deactivating_a_session_notifies_everyone_who_saved_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var saver = await SeedApprovedVisitorAsync();
        await SeedFavouriteAsync(session.Id, saver.Id);

        var delete = await DeleteAuthAsync($"/api/v1/admin/sessions/{session.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var row = await identity.Notifications.AsNoTracking().SingleAsync(notification =>
            notification.UserId == saver.Id
            && notification.Kind == NotificationKind.SessionCancelled);
        Assert.Equal(session.Id, row.RelatedEntityId);
        Assert.Contains(session.Title, row.Body);
        Assert.Contains(session.Start.FormatSaudi(), row.Body);
        Assert.DoesNotContain("UTC", row.Body);
        Assert.False(string.IsNullOrWhiteSpace(row.BodyArabic));

        // In-app AND email — the whole point of B2 is that the card no longer just
        // vanishes in silence.
        Assert.Contains(_factory.Emails.Messages, message => message.To == saver.Email);

        var audit = FindAudit(AuditEvents.SessionDeactivated, $"id={session.Id}");
        Assert.NotNull(audit);
        Assert.Contains("notified=1", audit!.Detail);
    }

    [Fact]
    public async Task B2_A_session_nobody_saved_is_cancelled_without_notifying_anyone()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);

        var delete = await DeleteAuthAsync($"/api/v1/admin/sessions/{session.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.False(await identity.Notifications.AnyAsync(notification =>
            notification.Kind == NotificationKind.SessionCancelled
            && notification.RelatedEntityId == session.Id));
    }

    [Fact]
    public async Task B2_Unticking_Active_on_the_edit_form_notifies_exactly_like_Deactivate()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var saver = await SeedApprovedVisitorAsync();
        await SeedFavouriteAsync(session.Id, saver.Id);

        // SessionsAddEdit.razor renders an Active checkbox on the edit form and the
        // PUT carries its value, so this is a fully reachable cancellation path. It
        // used to flip IsActive and tell nobody, while the Deactivate action notified.
        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, isActive: false),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var row = await identity.Notifications.AsNoTracking().SingleAsync(notification =>
            notification.UserId == saver.Id
            && notification.Kind == NotificationKind.SessionCancelled);
        Assert.Equal(session.Id, row.RelatedEntityId);
        Assert.Contains(session.Title, row.Body);
        Assert.Contains(session.Start.FormatSaudi(), row.Body);
        Assert.DoesNotContain("UTC", row.Body);
        Assert.False(string.IsNullOrWhiteSpace(row.BodyArabic));
        Assert.Contains(_factory.Emails.Messages, message => message.To == saver.Email);

        var audit = FindAudit(AuditEvents.SessionDeactivated, $"id={session.Id}");
        Assert.NotNull(audit);
        Assert.Contains("notified=1", audit!.Detail);
    }

    [Fact]
    public async Task B2_An_edit_that_leaves_Active_ticked_announces_no_cancellation()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);
        var saver = await SeedApprovedVisitorAsync();
        await SeedFavouriteAsync(session.Id, saver.Id);

        var response = await PutAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}",
            UpdateFrom(session, title: "Renamed only"),
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // An ordinary save is not a cancellation — no notice, no deactivation audit.
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.False(await identity.Notifications.AnyAsync(notification =>
            notification.Kind == NotificationKind.SessionCancelled
            && notification.RelatedEntityId == session.Id));
        Assert.Null(FindAudit(AuditEvents.SessionDeactivated, $"id={session.Id}"));
    }

    // -- A37: a hall in use cannot be deactivated ----------------------------

    [Fact]
    public async Task A37_Deactivating_a_hall_active_sessions_use_is_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hall = await SeedHallAsync(capacity: 50);
        var session = await CreateSessionAsync(token, hall.Id);

        var delete = await DeleteAuthAsync($"/api/v1/admin/halls/{hall.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallInUse, body.Error!.Code);
        Assert.Contains("1 active session", body.Error.Message);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        // The hall is untouched — the old code flipped it and the breakage only
        // surfaced later, as a 400 SESSION_HALL_NOT_FOUND on the next session edit.
        var read = await GetAuthAsync($"/api/v1/admin/halls/{hall.Id}", token);
        var stored = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
        Assert.True(stored.IsActive);

        // Re-home the session first and the hall deactivates normally.
        Assert.Equal(HttpStatusCode.OK,
            (await DeleteAuthAsync($"/api/v1/admin/sessions/{session.Id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await DeleteAuthAsync($"/api/v1/admin/halls/{hall.Id}", token)).StatusCode);
    }

    // -- helpers -------------------------------------------------------------

    private static string NewCode() =>
        "S-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<AdminSessionDetail> CreateSessionAsync(string token, Guid hallId)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = NewCode(),
                Title = "Lifecycle session",
                TitleArabic = "جلسة دورة الحياة",
                // An Event needs no speaker, so the create rules (#3 / #4) pass.
                Type = SessionType.Event,
                HallId = hallId,
                Start = DateTimeOffset.UtcNow.AddHours(1),
                End = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private async Task<AdminSessionDetail> GetSessionAsync(Guid id, string token)
    {
        var response = await GetAuthAsync($"/api/v1/admin/sessions/{id}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private static AdminUpdateSessionRequest UpdateFrom(
        AdminSessionDetail created,
        Guid? hallId = null,
        string? title = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        bool? isActive = null) =>
        new()
        {
            Code = created.Code,
            Title = title ?? created.Title,
            TitleArabic = created.TitleArabic,
            HallId = hallId ?? created.HallId,
            Start = start ?? created.Start,
            End = end ?? created.End,
            CapacityOverride = created.CapacityOverride,
            IsActive = isActive ?? created.IsActive,
            Type = created.Type,
        };

    private async Task<Hall> SeedHallAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Lifecycle Hall",
            NameArabic = "قاعة دورة الحياة",
            Capacity = capacity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task SeedReservationAsync(
        Guid sessionId, Guid? reservedForUserId, string row, int seat)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = reservedForUserId is null
                ? SeatReservationKind.AdminReservedRow
                : SeatReservationKind.UserBooking,
            ReservedForUserId = reservedForUserId,
            CreatedByUserId = reservedForUserId ?? Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = BookingStatus.Approved,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedFavouriteAsync(Guid sessionId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionFavourites.Add(new SessionFavourite
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Stamps the two worker "already done" flags so a test can prove a
    /// reschedule clears them (A4).</summary>
    private async Task StampWorkerFlagsAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await db.Sessions.SingleAsync(row => row.Id == sessionId);
        session.ReminderSent = DateTimeOffset.UtcNow;
        session.RatingPromptSent = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<Session> LoadSessionAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.Sessions.AsNoTracking().SingleAsync(row => row.Id == sessionId);
    }

    private SIMF.Domain.Auditing.OperationLogEntry? FindAudit(string eventType, string detailFragment)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return db.OperationLog.AsNoTracking()
            .Where(entry => entry.EventType == eventType)
            .AsEnumerable()
            .FirstOrDefault(entry => entry.Detail != null && entry.Detail.Contains(detailFragment));
    }

    private async Task<SimfUser> SeedApprovedVisitorAsync()
    {
        var email = $"lifecycle-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Lifecycle Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"lifecycle-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Lifecycle Admin",
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
                Email = email,
                Password = AuthFlow.Password,
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
