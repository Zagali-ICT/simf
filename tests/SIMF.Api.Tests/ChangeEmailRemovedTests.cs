// G1 (2026-07-30) — the self-service change-email feature was REMOVED by owner
// decision: the Flutter screen, the two endpoints, EmailChangeService and the
// EmailChange contracts are all deleted. An email can now only be changed by an
// administrator through the Control Panel account-edit form, which stays gated on
// Visitors.Edit / Others.Edit (AdminOnly).
//
// The removed endpoints were authenticated but carried NO PermissionCatalog
// policy, so any approved bearer token could call them. Deleting only the app
// screen would have left the feature callable. This file is the regression guard:
// the routes must be UNMAPPED (404), not merely hidden — a 200/401/403 here would
// mean the endpoints were reinstated.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ChangeEmailRemovedTests : IClassFixture<SimfApiFactory>
{
    private const string SendOtpUrl = "/api/v1/app/auth/change-email/send-otp";
    private const string ConfirmUrl = "/api/v1/app/auth/change-email/confirm";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ChangeEmailRemovedTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Send_otp_route_is_gone_and_returns_404_for_an_approved_caller()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        var send = await PostAuthAsync(
            SendOtpUrl,
            new { newEmail = $"new-{Guid.NewGuid():N}@simf.test" },
            token);

        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
    }

    [Fact]
    public async Task Confirm_route_is_gone_and_returns_404_for_an_approved_caller()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new
            {
                newEmail = $"new-{Guid.NewGuid():N}@simf.test",
                code = "123456",
                currentPassword = AuthFlow.Password,
            },
            token);

        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
    }

    [Fact]
    public async Task Change_email_routes_are_gone_for_an_anonymous_caller_too()
    {
        // A 401 here would mean the route still exists behind the auth filter; a
        // 404 proves it is not mapped at all.
        var send = await _client.PostAsJsonAsync(
            SendOtpUrl, new { newEmail = "x@simf.test" });
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);

        var confirm = await _client.PostAsJsonAsync(
            ConfirmUrl, new { newEmail = "x@simf.test", code = "123456" });
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
    }

    [Fact]
    public async Task A_change_email_call_no_longer_issues_a_verification_code()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();

        await PostAuthAsync(
            SendOtpUrl,
            new { newEmail = $"new-{Guid.NewGuid():N}@simf.test" },
            token);

        // AccountCodePurpose.EmailChangeVerification stays in the frozen enum
        // (D-110, persisted BY NAME) so existing rows still deserialize, but no
        // code path writes it any more.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await db.AccountCodes.CountAsync(c =>
            c.UserId == userId
            && c.Purpose == AccountCodePurpose.EmailChangeVerification);
        Assert.Equal(0, count);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<(string accessToken, Guid userId)> CreateApprovedVisitorAsync()
    {
        var email = $"cer-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "CER Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (envelope.Data!.Tokens!.AccessToken, userId);
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
