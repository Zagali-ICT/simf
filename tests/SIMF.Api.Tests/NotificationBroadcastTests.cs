// D-132 — admin broadcast-Notifications desk (Announcements). Covers the create
// endpoint + validation, the recipient estimate, and the background fan-out
// (INotificationBroadcastService.ProcessNextPendingAsync) which the hosted worker
// drives in production. The worker's startup Task.Delay runs on the FakeTimeProvider
// so it never fires during a test — the fan-out here is driven manually and is
// therefore deterministic.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class NotificationBroadcastTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationBroadcastTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Session_broadcast_dispatches_in_app_and_email_to_each_reserved_user()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var u1 = await SeedUserAsync(AccountState.Approved, UserType.Visitor);
        var u2 = await SeedUserAsync(AccountState.Approved, UserType.Visitor);
        var sessionId = await SeedSessionWithReservationsAsync(u1, u2);

        var create = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Session",
                SessionId = sessionId,
                Title = "Hall changed",
                TitleArabic = "تغيّرت القاعة",
                Body = "Your session has moved to Hall B.",
                BodyArabic = "انتقلت جلستك إلى القاعة ب.",
                Severity = "Warning",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var result = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBroadcastResult>>())!.Data!;
        Assert.Equal(2, result.EstimatedRecipients);

        await DrainBroadcastsAsync();

        // Each reserved user got exactly one in-app row tied to the session.
        Assert.Equal(1, await CountAnnouncementsAsync(u1, sessionId));
        Assert.Equal(1, await CountAnnouncementsAsync(u2, sessionId));

        var detail = await GetBroadcastAsync(result.Id, token);
        Assert.Equal("Completed", detail.Status);
        Assert.Equal(2, detail.TotalRecipients);
        Assert.Equal(2, detail.Dispatched);
        Assert.Equal(2, detail.EmailsEnqueued);
        Assert.Equal(0, detail.Skipped);
    }

    [Fact]
    public async Task Audience_ApprovedAppUsers_reaches_approved_visitor_not_admin_or_pending()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var approved = await SeedUserAsync(AccountState.Approved, UserType.Visitor);
        var pending = await SeedUserAsync(AccountState.PendingApproval, UserType.Visitor);
        var admin = await SeedUserAsync(AccountState.Approved, UserType.Admin);

        var create = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Audience",
                AudienceScope = "ApprovedAppUsers",
                Title = "Welcome",
                TitleArabic = "أهلاً",
                Body = "The forum starts tomorrow.",
                BodyArabic = "يبدأ الملتقى غداً.",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        await DrainBroadcastsAsync();

        Assert.True(await CountAnnouncementsAsync(approved) >= 1);
        Assert.Equal(0, await CountAnnouncementsAsync(pending));
        Assert.Equal(0, await CountAnnouncementsAsync(admin));
    }

    [Fact]
    public async Task Estimate_session_returns_active_reserved_count()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var u1 = await SeedUserAsync(AccountState.Approved, UserType.Visitor);
        var u2 = await SeedUserAsync(AccountState.Approved, UserType.Visitor);
        var sessionId = await SeedSessionWithReservationsAsync(u1, u2);

        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast/estimate",
            new AdminBroadcastEstimateRequest { TargetMode = "Session", SessionId = sessionId },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBroadcastEstimateResult>>())!.Data!;
        Assert.Equal(2, result.EstimatedRecipients);
    }

    [Fact]
    public async Task Session_mode_without_session_is_400_BROADCAST_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Session",
                SessionId = null,
                Title = "t", TitleArabic = "ع", Body = "b", BodyArabic = "ب",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BroadcastInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Session_mode_unknown_session_is_404_SESSION_NOT_FOUND()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Session",
                SessionId = Guid.NewGuid(),
                Title = "t", TitleArabic = "ع", Body = "b", BodyArabic = "ب",
            },
            token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Audience_mode_invalid_scope_is_400_BROADCAST_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Audience",
                AudienceScope = "Nonsense",
                Title = "t", TitleArabic = "ع", Body = "b", BodyArabic = "ب",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BroadcastInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Blank_title_is_rejected_by_validation()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Audience",
                AudienceScope = "ApprovedAppUsers",
                Title = "", TitleArabic = "ع", Body = "b", BodyArabic = "ب",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/notifications/broadcast",
            new AdminCreateBroadcastRequest
            {
                TargetMode = "Audience",
                AudienceScope = "ApprovedAppUsers",
                Title = "t", TitleArabic = "ع", Body = "b", BodyArabic = "ب",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Recover_stalled_marks_old_processing_broadcasts_failed()
    {
        var broadcastId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.NotificationBroadcasts.Add(new NotificationBroadcast
            {
                Id = broadcastId,
                CreatedByUserId = Guid.NewGuid(),
                TargetMode = BroadcastTargetMode.Audience,
                AudienceScope = BroadcastAudienceScope.ApprovedAppUsers,
                Title = "Stalled",
                TitleArabic = "معلّق",
                Body = "b",
                BodyArabic = "ب",
                Severity = NotificationSeverity.Info,
                Status = BroadcastStatus.Processing,
                CreatedAt = SimfClock.Now.AddHours(-1),
                // Older than the 15-minute stalled cutoff.
                StartedAt = SimfClock.Now.AddHours(-1),
            });
            await db.SaveChangesAsync();
        }

        int recovered;
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<INotificationBroadcastService>();
            recovered = await service.RecoverStalledAsync(CancellationToken.None);
        }
        Assert.True(recovered >= 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.NotificationBroadcasts.AsNoTracking()
                .SingleAsync(b => b.Id == broadcastId);
            Assert.Equal(BroadcastStatus.Failed, row.Status);
            Assert.NotNull(row.Error);
            Assert.NotNull(row.CompletedAt);
        }
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task DrainBroadcastsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationBroadcastService>();
        while (await service.ProcessNextPendingAsync(CancellationToken.None))
        {
        }
    }

    private async Task<int> CountAnnouncementsAsync(Guid userId, Guid? relatedSessionId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var query = idDb.Notifications.Where(n =>
            n.Kind == NotificationKind.AdminAnnouncement && n.UserId == userId);
        if (relatedSessionId is { } sessionId)
        {
            query = query.Where(n => n.RelatedEntityId == sessionId);
        }
        return await query.CountAsync();
    }

    private async Task<AdminBroadcastSummary> GetBroadcastAsync(Guid id, string token)
    {
        var response = await GetAuthAsync($"/api/v1/admin/notifications/broadcasts/{id}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBroadcastSummary>>())!.Data!;
    }

    private async Task<Guid> SeedUserAsync(AccountState state, UserType type)
    {
        var email = $"bcast-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Broadcast User",
            AccountState = state,
            UserType = type,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<Guid> SeedSessionWithReservationsAsync(params Guid[] visitorIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall",
            NameArabic = "قاعة",
            Capacity = 50,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Panel",
            TitleArabic = "جلسة",
            HallId = hall.Id,
            Start = SimfClock.Now.AddHours(2),
            End = SimfClock.Now.AddHours(3),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        var seat = 1;
        foreach (var visitorId in visitorIds)
        {
            db.SeatReservations.Add(new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                RowLabel = "A",
                SeatNumber = seat++,
                Kind = SeatReservationKind.UserBooking,
                ReservedForUserId = visitorId,
                CreatedByUserId = visitorId,
                CreatedAt = SimfClock.Now,
            });
        }
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"bcast-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Broadcast Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
