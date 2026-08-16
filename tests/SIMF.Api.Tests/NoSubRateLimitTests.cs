// D-188 (D-181 carry-over closure) — proves the `ai-test:no-sub`
// rate-limit partition actually bounds requests. The original D-181
// review-pass replaced `GetNoLimiter` with `FixedWindowRateLimiter`
// (5 permits / 1 min) on the no-sub branch as defense-in-depth
// against a future JWT-claim-mapper regression. The D-181 backlog
// flagged that we lacked a behavior test for this branch.
//
// .NET's built-in FixedWindowRateLimiter does NOT accept a
// TimeProvider for testability — the limiter computes its window
// internally from the real clock. We test the boundary directly
// (six sequential calls in well under one minute) without trying to
// abstract time. The 6th call gets 429; that is the contract.
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SIMF.Common;
using SIMF.Contracts.Ai;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class NoSubRateLimitTests : IClassFixture<SimfApiFactory>
{
    // Must match SimfApiFactory's Jwt__SigningKey env var; the test
    // host validates JWTs signed with this key.
    private const string SigningKey =
        "ytlV1+ke14Pw900IRtH8zT4uIKBeaqjcj6aFfiLozS5jKgSs";
    private const string Issuer = "SIMF";
    private const string Audience = "SIMF";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NoSubRateLimitTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Six_requests_without_sub_claim_trip_the_no_sub_partition()
    {
        // Mint a JWT that authenticates as Administrator but carries
        // an empty `sub` claim — the exact shape that a JWT-claim-
        // mapper regression would produce. The Administrator policy
        // is role-based (RequireRole("Administrator")), so the token
        // still passes that gate; the rate-limit policy then reads
        // sub, finds it null/empty, and routes to the "ai-test:no-sub"
        // partition (5 permits / 1 min per D-181).
        var token = MintTokenWithEmptySub();

        // Any AI Test endpoint id works — the test endpoint is the
        // one the `ai-test` policy is wired to. Use an obviously
        // bogus id so the call lands on the rate-limit middleware
        // (not the prompt lookup). The request will return a 4xx
        // for any reason that's NOT 429; the 6th request must be
        // 429 because the no-sub partition is full by then.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await PostTestAsync(token);
            statuses.Add(response.StatusCode);
        }

        // The first 5 calls fit inside the 5-permit window — any
        // outcome other than 429 is acceptable (likely 404 because
        // the bogus prompt id is unknown, or 400 for missing inputs).
        // The 6th must be 429 — that's the defense-in-depth bound
        // we're proving works.
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(5));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);
    }

    private Task<HttpResponseMessage> PostTestAsync(string token)
    {
        var url = $"/api/v1/admin/ai/prompts/{Guid.NewGuid()}/test";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new TestAiPromptRequest
            {
                Inputs = new Dictionary<string, string>(),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static string MintTokenWithEmptySub()
    {
        var now = SimfClock.Now;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Deliberately omit JwtRegisteredClaimNames.Sub. Carry the
        // Administrator role + the security_stamp + account_state
        // claims the authorization handlers need.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, "no-sub-admin@simf.test"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("display_name", "No-Sub Admin"),
            new("security_stamp", "test-stamp-no-sub"),
            new("account_state", "Approved"),
            new("user_type", "Admin"),
            new("mobile_app_role", "None"),
            new(ClaimTypes.Role, "Administrator"),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
