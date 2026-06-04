using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for <c>GET /api/v1/app/users/me</c> (Page 011
/// registration-status read, D-249). The endpoint is available to any
/// signed-in account — including not-yet-approved ones — so the app can poll
/// the approval state; the six-value <see cref="AccountState"/> collapses onto
/// the app's three-value <c>{Pending, Approved, Rejected}</c> vocabulary.
/// </summary>
public sealed class CurrentUserEndpointTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public CurrentUserEndpointTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Me_returns_identity_app_role_and_pending_status_for_a_verified_visitor()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var body = await GetMeAsync(tokens.AccessToken);

        Assert.True(body.Success);
        Assert.Equal(tokens.User.Id, body.Data!.Id);
        Assert.Equal(tokens.User.Email, body.Data.Email);
        Assert.Equal("Visitor", body.Data.AppRole);
        Assert.Equal("ar", body.Data.PreferredLanguage);
        // A freshly email-verified, not-yet-approved account reads as Pending.
        Assert.Equal("Pending", body.Data.RegistrationStatus);
    }

    [Fact]
    public async Task Me_reflects_an_approved_account_as_Approved()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        AuthFlow.SetAccountState(_factory, tokens.User.Email, AccountState.Approved);

        var body = await GetMeAsync(tokens.AccessToken);

        Assert.Equal("Approved", body.Data!.RegistrationStatus);
    }

    [Fact]
    public async Task Me_reflects_a_rejected_account_as_Rejected()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        AuthFlow.SetAccountState(_factory, tokens.User.Email, AccountState.Rejected);

        var body = await GetMeAsync(tokens.AccessToken);

        Assert.Equal("Rejected", body.Data!.RegistrationStatus);
    }

    [Fact]
    public async Task Me_without_a_bearer_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/app/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<ApiResult<CurrentUserResponse>> GetMeAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
    }
}
