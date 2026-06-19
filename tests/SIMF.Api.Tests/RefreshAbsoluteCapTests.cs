using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-443 (NCA finding): a session is capped at an absolute 24h from sign-in.
/// Refresh-token rotation carries the chain's original deadline forward instead
/// of sliding a fresh window, so a user who keeps refreshing is still forced to
/// re-sign-in once 24h elapse. Isolated in its own fixture because it advances
/// the test clock (which would future-date tokens issued by other tests).
/// </summary>
public sealed class RefreshAbsoluteCapTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RefreshAbsoluteCapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Rotation_does_not_slide_the_session_past_the_24h_cap()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        // 23h in — still inside the 24h cap: a refresh succeeds and rotates.
        _factory.Time.Advance(TimeSpan.FromHours(23));
        var rotated = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var rotatedBody = await rotated.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>();
        var rotatedRefreshToken = rotatedBody!.Data!.RefreshToken;

        // 2h further (25h total — past the original sign-in + 24h deadline). The
        // rotated token inherited that same deadline, so it is now expired. Had
        // rotation slid a fresh 24h window this refresh would still succeed —
        // that is exactly the regression this test guards against.
        _factory.Time.Advance(TimeSpan.FromHours(2));
        var afterCap = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh",
            new RefreshRequest { RefreshToken = rotatedRefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterCap.StatusCode);
        var capBody = await afterCap.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthRefreshTokenExpired, capBody!.Error!.Code);
    }
}
