using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Notifications;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for the P12 — D-053 notifications surface
/// (list / unread-count / mark-read / mark-all / delete + actor-scoped
/// isolation).</summary>
public sealed class NotificationTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_returns_only_the_actors_own_notifications_newest_first()
    {
        var (token, userId) = await CreateUserAndSignInAsync();
        var otherUserId = await CreateOtherUserAsync();

        await SeedAsync(userId, "First", DateTimeOffset.UtcNow.AddMinutes(-2));
        await SeedAsync(userId, "Second", DateTimeOffset.UtcNow.AddMinutes(-1));
        await SeedAsync(otherUserId, "Not mine", DateTimeOffset.UtcNow);

        var response = await PostAuthAsync("/api/v1/account/notifications/list",
            new GridQuery { Top = 50 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<NotificationDto>>>())!.Data!;
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("Second", page.Items[0].Title);
        Assert.Equal("First", page.Items[1].Title);
    }

    [Fact]
    public async Task UnreadCount_only_counts_the_actors_unread_rows()
    {
        var (token, userId) = await CreateUserAndSignInAsync();
        await SeedAsync(userId, "A", DateTimeOffset.UtcNow);
        await SeedAsync(userId, "B", DateTimeOffset.UtcNow, readAt: DateTimeOffset.UtcNow);

        var response = await GetAuthAsync(
            "/api/v1/account/notifications/unread-count", token);
        var count = (await response.Content
            .ReadFromJsonAsync<ApiResult<UnreadCountResponse>>())!.Data!.UnreadCount;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkRead_flips_the_row_and_is_idempotent()
    {
        var (token, userId) = await CreateUserAndSignInAsync();
        var id = await SeedAsync(userId, "Z", DateTimeOffset.UtcNow);

        var first = await PostAuthAsync(
            $"/api/v1/account/notifications/{id}/read", new { }, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var row = await db.Notifications.SingleAsync(n => n.Id == id);
        Assert.NotNull(row.ReadAt);

        // Second call is a no-op (still OK).
        var second = await PostAuthAsync(
            $"/api/v1/account/notifications/{id}/read", new { }, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task MarkAllRead_clears_every_unread()
    {
        var (token, userId) = await CreateUserAndSignInAsync();
        await SeedAsync(userId, "A", DateTimeOffset.UtcNow);
        await SeedAsync(userId, "B", DateTimeOffset.UtcNow);
        await SeedAsync(userId, "C", DateTimeOffset.UtcNow);

        var response = await PostAuthAsync(
            "/api/v1/account/notifications/read-all", new { }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var count = await GetUnreadCountAsync(token);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Delete_removes_the_row_and_is_idempotent()
    {
        var (token, userId) = await CreateUserAndSignInAsync();
        var id = await SeedAsync(userId, "Doomed", DateTimeOffset.UtcNow);

        var first = await DeleteAuthAsync(
            $"/api/v1/account/notifications/{id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await DeleteAuthAsync(
            $"/api/v1/account/notifications/{id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.False(await db.Notifications.AnyAsync(n => n.Id == id));
    }

    [Fact]
    public async Task Dispatcher_writes_a_DB_row_when_called()
    {
        var (token, userId) = await CreateUserAndSignInAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider
                .GetRequiredService<INotificationDispatcher>();
            await dispatcher.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.AccountApproved,
                Title = "Hello",
                TitleArabic = "مرحباً",
                Body = "Body en",
                BodyArabic = "نص عربي",
                Severity = NotificationSeverity.Success,
                SendEmail = false,
            });
        }

        var count = await GetUnreadCountAsync(token);
        Assert.Equal(1, count);
    }

    // -- helpers ---------------------------------------------------------------

    private async Task<int> GetUnreadCountAsync(string token)
    {
        var response = await GetAuthAsync(
            "/api/v1/account/notifications/unread-count", token);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<UnreadCountResponse>>())!.Data!.UnreadCount;
    }

    private async Task<Guid> SeedAsync(
        Guid userId, string title, DateTimeOffset createdAt,
        DateTimeOffset? readAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var row = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = NotificationKind.AccountProfileSubmitted,
            Title = title,
            TitleArabic = title,
            Body = "body",
            BodyArabic = "body",
            Severity = NotificationSeverity.Info,
            CreatedAt = createdAt,
            ReadAt = readAt,
        };
        db.Notifications.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<(string Token, Guid UserId)> CreateUserAndSignInAsync()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        // D-099: sign-up now dispatches a "Verification code sent" in-app
        // notification. Clear that pre-seeded row so the per-test seeders +
        // count assertions start from a clean slate.
        await ClearNotificationsAsync(tokens.User.Id);
        return (tokens.AccessToken, tokens.User.Id);
    }

    private async Task ClearNotificationsAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync();
    }

    private async Task<Guid> CreateOtherUserAsync()
    {
        var email = $"other-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Other",
            AccountState = AccountState.Approved,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
