using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SIMF.Application.Auditing;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests that the JWT bearer middleware rejects every kind of
/// invalid access token (SIMF-API-001 section 12). Each test mints a token
/// directly and presents it at a protected endpoint.
/// </summary>
public sealed class JwtMiddlewareTests : IClassFixture<SimfApiFactory>
{
    // Must match SimfApiFactory's Jwt__SigningKey and the configured issuer/audience.
    private const string SigningKey = "ytlV1+ke14Pw900IRtH8zT4uIKBeaqjcj6aFfiLozS5jKgSs";
    private const string Issuer = "SIMF";
    private const string Audience = "SIMF";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public JwtMiddlewareTests(SimfApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public Task A_token_with_a_forged_signature_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            "a-completely-different-and-wrong-signing-key-000",
            Issuer, Audience, SimfClock.Now.AddMinutes(30)));

    [Fact]
    public Task A_token_with_the_wrong_issuer_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, "not-simf", Audience, SimfClock.Now.AddMinutes(30)));

    [Fact]
    public Task A_token_with_the_wrong_audience_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, Issuer, "not-simf", SimfClock.Now.AddMinutes(30)));

    [Fact]
    public Task An_expired_token_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, Issuer, Audience, SimfClock.Now.AddHours(-1)));

    [Fact]
    public Task A_malformed_token_is_rejected() =>
        AssertRejectedAsync("this-is-not-a-valid-jwt");

    // ----------------------------------------------------------------------
    // H5 — D-060: OnTokenValidated requires the security_stamp claim to be
    // present and non-empty. A token without it (or carrying an empty one)
    // bypasses the revocation check today — sign-out, password-change and
    // 2FA-reset cannot invalidate it. Reject up-front.
    // ----------------------------------------------------------------------

    [Fact]
    public Task A_token_without_a_security_stamp_claim_is_rejected() =>
        AssertRejectedAsync(MakeTokenWithoutStamp(
            SigningKey, Issuer, Audience, SimfClock.Now.AddMinutes(30)));

    [Fact]
    public Task A_token_with_an_empty_security_stamp_claim_is_rejected() =>
        AssertRejectedAsync(MakeTokenWithStamp(
            SigningKey, Issuer, Audience, SimfClock.Now.AddMinutes(30),
            stamp: string.Empty));

    // ----------------------------------------------------------------------
    // H26 — D-086: per-IP bearer-rejection audit-DB throttle.
    // An attacker flooding bearer-garbage requests cannot force one DB
    // INSERT per request. The 11th rejection from the same IP inside a
    // 60-second window drops the audit row (the structured Serilog
    // line still fires so SOC's log forwarder sees the storm). Cap is
    // MaxAuditWritesPerWindow = 10 in JwtBearerSetup.cs.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Bearer_rejection_audit_writes_are_capped_per_ip_within_the_window()
    {
        // Clear any pre-test rejection counter so the cap math is
        // deterministic regardless of preceding tests' bearer rejections.
        using (var scope = _factory.Services.CreateScope())
        {
            // IMemoryCache doesn't expose Clear; the test-host IP is
            // typically "::1" / "127.0.0.1" / null — remove all variants.
            var cache = scope.ServiceProvider.GetService<IMemoryCache>();
            if (cache is not null)
            {
                cache.Remove("simf-bearer-rej:127.0.0.1");
                cache.Remove("simf-bearer-rej:::1");
                cache.Remove("simf-bearer-rej:unknown");
            }
        }

        // Snapshot the audit-log count before.
        int Count() {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            return db.OperationLog.Count(e => e.EventType == AuditEvents.AccessTokenRejected);
        }
        var before = Count();

        // Fire 15 rejected-bearer requests — well above the 10/min cap.
        var malformedToken = "this-is-not-a-valid-jwt";
        for (var i = 0; i < 15; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/app/auth/sign-out");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", malformedToken);
            var resp = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // Cap is 10; the request count above is 15, so the delta must be
        // <= 10. Strict upper bound — sane regardless of any concurrent
        // bearer rejections that other tests in the same suite may have
        // emitted (they would only ADD pre-existing rows, never invert
        // the cap relationship).
        var after = Count();
        var delta = after - before;
        Assert.True(delta <= 10,
            $"Expected at most 10 audit rows added by the 15-request burst, got {delta}.");
        // Sanity: at least one row should have landed (the throttle does
        // not silently swallow all writes).
        Assert.True(delta >= 1,
            $"Expected at least 1 audit row in the burst, got {delta}.");
    }

    private async Task AssertRejectedAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/app/auth/sign-out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string MakeToken(
        string signingKey,
        string issuer,
        string audience,
        DateTime expires) =>
        MakeTokenWithStamp(signingKey, issuer, audience, expires, stamp: "x");

    private static string MakeTokenWithStamp(
        string signingKey,
        string issuer,
        string audience,
        DateTime expires,
        string stamp)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("security_stamp", stamp)],
            notBefore: expires.AddMinutes(-30),
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MakeTokenWithoutStamp(
        string signingKey,
        string issuer,
        string audience,
        DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore: expires.AddMinutes(-30),
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
