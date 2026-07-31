// D9 — D-249: GET /api/v1/app/bootstrap on-login bundle.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Account;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Notifications;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for <c>GET /api/v1/app/bootstrap</c> (D9 — D-249): the
/// on-login bundle (current user + unread-notification count + server time),
/// composed from existing reads and available to any signed-in account.
/// </summary>
public sealed class AppBootstrapTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AppBootstrapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bootstrap_returns_the_current_user_unread_count_and_server_time()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var data = await GetBootstrapAsync(tokens.AccessToken);

        Assert.Equal(tokens.User.Id, data.User.Id);
        Assert.Equal(tokens.User.Email, data.User.Email);
        Assert.Equal("Visitor", data.User.AppRole);
        Assert.Equal("Pending", data.User.RegistrationStatus);
        Assert.True(data.ServerTime > new DateTime(2026, 1, 1, 0, 0, 0));

        // The bundle's count is the same source as the dedicated endpoint. The
        // standalone endpoint now requires an approved account (finding #23), so
        // cross-check the "same source" invariant against an approved account.
        var approved = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);
        var approvedBundle = await GetBootstrapAsync(approved.AccessToken);
        Assert.Equal(
            await StandaloneUnreadCountAsync(approved.AccessToken),
            approvedBundle.UnreadNotificationCount);
    }

    [Fact]
    public async Task Bootstrap_unread_count_reflects_a_dispatched_notification()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var baseline = (await GetBootstrapAsync(tokens.AccessToken)).UnreadNotificationCount;

        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
            await dispatcher.DispatchAsync(new NotificationRequest
            {
                UserId = tokens.User.Id,
                Kind = NotificationKind.AccountApproved,
                Title = "Hello", TitleArabic = "مرحباً",
                Body = "Body", BodyArabic = "نص",
                Severity = NotificationSeverity.Success,
                SendEmail = false,
            });
        }

        var data = await GetBootstrapAsync(tokens.AccessToken);
        Assert.Equal(baseline + 1, data.UnreadNotificationCount);
    }

    [Fact]
    public async Task Bootstrap_reflects_an_approved_account()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        AuthFlow.SetAccountState(_factory, tokens.User.Email, AccountState.Approved);

        var data = await GetBootstrapAsync(tokens.AccessToken);
        Assert.Equal("Approved", data.User.RegistrationStatus);
    }

    [Fact]
    public async Task Bootstrap_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/app/bootstrap");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AppBootstrap> GetBootstrapAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/bootstrap");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AppBootstrap>>())!.Data!;
    }

    private async Task<int> StandaloneUnreadCountAsync(string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/app/account/notifications/unread-count");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<UnreadCountResponse>>())!.Data!.UnreadCount;
    }
}
