using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration test that the authentication endpoints are rate-limited
/// (SIMF-API-001 section 10). Uses <see cref="RateLimitedApiFactory"/>, whose
/// permit limit is 3.
/// </summary>
public sealed class RateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitTests(RateLimitedApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task The_auth_endpoints_return_429_once_the_limit_is_exceeded()
    {
        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            lastResponse = await _client.PostAsJsonAsync(
                "/api/v1/auth/sign-up",
                new SignUpRequest
                {
                    Email = $"rl-{Guid.NewGuid():N}@simf.test",
                    Password = "Passw0rd!",
                    ConfirmPassword = "Passw0rd!",
                });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        var body = await lastResponse.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.RateLimitExceeded, body!.Error!.Code);
    }
}
