using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the session lifecycle — refresh-token rotation and
/// reuse detection, sign-out, and the bearer authentication middleware
/// (SIMF-API-001 section 12.4).
/// </summary>
public sealed class SessionTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_rotates_the_refresh_token()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        var response = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = (await response.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_401()
    {
        var response = await RefreshAsync("not-a-real-refresh-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        // Rotate once — the original token is now revoked, the rotated one live.
        var rotated = (await (await RefreshAsync(tokens.RefreshToken)).Content
            .ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;

        // Present the original (revoked) token again — that is reuse.
        var reuse = await RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The family is revoked: even the legitimate rotated token now fails.
        var familyRevoked = await RefreshAsync(rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);
    }

    [Fact]
    public async Task A_disabled_account_cannot_refresh()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        AuthFlow.SetAccountState(_factory, tokens.User.Email, AccountState.Disabled);

        var response = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthAccountDisabled, body!.Error!.Code);
    }

    [Fact]
    public async Task Sign_out_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/v1/auth/sign-out", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_with_a_valid_access_token_succeeds()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        var response = await SignOutAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_revokes_the_refresh_token()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        await SignOutAsync(tokens.AccessToken);

        var refresh = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task An_access_token_is_rejected_after_sign_out()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        await SignOutAsync(tokens.AccessToken);

        // The same access token, reused after sign-out — the security stamp moved.
        var response = await SignOutAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_successful_refresh_writes_a_RefreshTokenRotated_audit_entry()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        await RefreshAsync(tokens.RefreshToken);

        Assert.True(AuthFlow.AuditEntryExists(
            _factory, tokens.User.Email, AuditEvents.RefreshTokenRotated));
    }

    [Fact]
    public async Task Refresh_token_reuse_writes_a_RefreshTokenReused_audit_entry()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        await RefreshAsync(tokens.RefreshToken);

        await RefreshAsync(tokens.RefreshToken);

        Assert.True(AuthFlow.AuditEntryExists(
            _factory, tokens.User.Email, AuditEvents.RefreshTokenReused));
    }

    [Fact]
    public async Task Sign_out_writes_a_SignOutSucceeded_audit_entry()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        await SignOutAsync(tokens.AccessToken);

        Assert.True(AuthFlow.AuditEntryExists(
            _factory, tokens.User.Email, AuditEvents.SignOutSucceeded));
    }

    // -- helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = refreshToken });

    private Task<HttpResponseMessage> SignOutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
