using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Contracts.Authentication;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration test that the authentication endpoints are rate-limited
/// (SIMF-API-001 section 10) and that a rejection is recorded in the operation
/// log. Uses <see cref="RateLimitedApiFactory"/>, whose permit limit is 3.
/// </summary>
public sealed class RateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;
    private readonly HttpClient _client;

    public RateLimitTests(RateLimitedApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task The_auth_endpoints_admit_up_to_the_limit_then_reject_and_audit()
    {
        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/app/auth/sign-up",
                new SignUpRequest
                {
                    Email = $"rl-{Guid.NewGuid():N}@simf.test",
                    Password = "Passw0rd!",
                    ConfirmPassword = "Passw0rd!",
                });
            statuses.Add(response.StatusCode);
        }

        // The permit limit is 3 — the first three are admitted, the rest rejected.
        Assert.Equal(HttpStatusCode.Created, statuses[0]);
        Assert.Equal(HttpStatusCode.Created, statuses[2]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[3]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[4]);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Contains(
            database.OperationLog,
            entry => entry.EventType == AuditEvents.RateLimitRejected);
    }
}
