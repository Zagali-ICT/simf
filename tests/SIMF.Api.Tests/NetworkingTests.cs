// B6 — D-224: visitor-to-visitor networking connections (request/accept).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Networking;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class NetworkingTests : IClassFixture<SimfApiFactory>
{
    private const string Path = "/api/v1/app/account/connections";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NetworkingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_creates_pending_and_shows_in_both_parties_lists()
    {
        var (aliceToken, _) = await CreateApprovedVisitorAsync();
        var (bobToken, bobId) = await CreateApprovedVisitorAsync();

        var request = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = bobId }, aliceToken);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var result = (await request.Content
            .ReadFromJsonAsync<ApiResult<ConnectionResult>>())!.Data!;
        Assert.Equal(ConnectionState.Pending, result.State);

        // Alice's list: outgoing.
        var aliceList = await ListAsync(aliceToken);
        var aliceRow = Assert.Single(aliceList, r => r.Id == result.Id);
        Assert.False(aliceRow.IsIncoming);

        // Bob's list: incoming + pending.
        var bobList = await ListAsync(bobToken);
        var bobRow = Assert.Single(bobList, r => r.Id == result.Id);
        Assert.True(bobRow.IsIncoming);
        Assert.Equal(ConnectionState.Pending, bobRow.State);
    }

    [Fact]
    public async Task Self_connect_is_rejected()
    {
        var (aliceToken, aliceId) = await CreateApprovedVisitorAsync();
        var response = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = aliceId }, aliceToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ConnectionSelf, body.Error!.Code);
    }

    [Fact]
    public async Task Unknown_target_is_404()
    {
        var (aliceToken, _) = await CreateApprovedVisitorAsync();
        var response = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = Guid.NewGuid() }, aliceToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ConnectionTargetNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_pair_is_rejected_in_either_direction()
    {
        var (aliceToken, aliceId) = await CreateApprovedVisitorAsync();
        var (bobToken, bobId) = await CreateApprovedVisitorAsync();

        var first = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = bobId }, aliceToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same direction again.
        var again = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = bobId }, aliceToken);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Reverse direction.
        var reverse = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = aliceId }, bobToken);
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
        var body = (await reverse.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ConnectionAlreadyExists, body.Error!.Code);
    }

    [Fact]
    public async Task Only_the_target_can_accept()
    {
        var (aliceToken, _) = await CreateApprovedVisitorAsync();
        var (bobToken, bobId) = await CreateApprovedVisitorAsync();
        var connectionId = await RequestAsync(aliceToken, bobId);

        // The requester cannot accept their own request.
        var byAlice = await PutAuthAsync($"{Path}/{connectionId}/accept", aliceToken);
        Assert.Equal(HttpStatusCode.Forbidden, byAlice.StatusCode);

        // The target can.
        var byBob = await PutAuthAsync($"{Path}/{connectionId}/accept", bobToken);
        Assert.Equal(HttpStatusCode.OK, byBob.StatusCode);
        var result = (await byBob.Content
            .ReadFromJsonAsync<ApiResult<ConnectionResult>>())!.Data!;
        Assert.Equal(ConnectionState.Accepted, result.State);

        // Re-accepting is idempotent.
        var again = await PutAuthAsync($"{Path}/{connectionId}/accept", bobToken);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Remove_soft_deletes_and_drops_from_both_lists()
    {
        var (aliceToken, _) = await CreateApprovedVisitorAsync();
        var (bobToken, bobId) = await CreateApprovedVisitorAsync();
        var connectionId = await RequestAsync(aliceToken, bobId);
        await PutAuthAsync($"{Path}/{connectionId}/accept", bobToken);

        var remove = await DeleteAuthAsync($"{Path}/{connectionId}", aliceToken);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        Assert.DoesNotContain(await ListAsync(aliceToken), r => r.Id == connectionId);
        Assert.DoesNotContain(await ListAsync(bobToken), r => r.Id == connectionId);

        // After removal the pair can be requested again (soft-delete does not block).
        var reRequest = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = bobId }, aliceToken);
        Assert.Equal(HttpStatusCode.OK, reRequest.StatusCode);
    }

    [Fact]
    public async Task Accept_missing_connection_is_404()
    {
        var (token, _) = await CreateApprovedVisitorAsync();
        var response = await PutAuthAsync($"{Path}/{Guid.NewGuid()}/accept", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ConnectionNotFound, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> RequestAsync(string token, Guid targetUserId)
    {
        var response = await PostAuthAsync(Path,
            new SendConnectionRequest { TargetUserId = targetUserId }, token);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<ConnectionResult>>())!.Data!.Id;
    }

    private async Task<IReadOnlyList<ConnectionRow>> ListAsync(string token)
    {
        var response = await GetAuthAsync(Path, token);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ConnectionRow>>>())!.Data!;
    }

    private async Task<(string AccessToken, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"net-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        // D-373 — registration enables 2FA; this auth plumbing needs the
        // direct-token path (the admin-disabled scenario).
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, UserIdFromToken(token));
    }

    private static Guid UserIdFromToken(string accessToken)
    {
        var payload = accessToken.Split('.')[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
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

    private Task<HttpResponseMessage> PutAuthAsync(string url, string token)
    {
        // Route-only PUT — FastEndpoints still expects a JSON content-type for
        // the bound request DTO, so send an empty JSON body (mirrors how the
        // route-only POST endpoints are exercised elsewhere).
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new { }),
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
