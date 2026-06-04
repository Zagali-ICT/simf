using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// The expired-refresh-token test is isolated in its own fixture: it advances
/// the test clock, which would future-date tokens issued by other tests sharing
/// the same factory.
/// </summary>
public sealed class RefreshExpiryTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RefreshExpiryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_with_an_expired_token_returns_401()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        // The refresh token lives 30 days — move past it.
        _factory.Time.Advance(TimeSpan.FromDays(31));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthRefreshTokenExpired, body!.Error!.Code);
    }
}
