using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

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

    private readonly HttpClient _client;

    public JwtMiddlewareTests(SimfApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public Task A_token_with_a_forged_signature_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            "a-completely-different-and-wrong-signing-key-000",
            Issuer, Audience, DateTimeOffset.UtcNow.AddMinutes(30)));

    [Fact]
    public Task A_token_with_the_wrong_issuer_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, "not-simf", Audience, DateTimeOffset.UtcNow.AddMinutes(30)));

    [Fact]
    public Task A_token_with_the_wrong_audience_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, Issuer, "not-simf", DateTimeOffset.UtcNow.AddMinutes(30)));

    [Fact]
    public Task An_expired_token_is_rejected() =>
        AssertRejectedAsync(MakeToken(
            SigningKey, Issuer, Audience, DateTimeOffset.UtcNow.AddHours(-1)));

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
            SigningKey, Issuer, Audience, DateTimeOffset.UtcNow.AddMinutes(30)));

    [Fact]
    public Task A_token_with_an_empty_security_stamp_claim_is_rejected() =>
        AssertRejectedAsync(MakeTokenWithStamp(
            SigningKey, Issuer, Audience, DateTimeOffset.UtcNow.AddMinutes(30),
            stamp: string.Empty));

    private async Task AssertRejectedAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string MakeToken(
        string signingKey,
        string issuer,
        string audience,
        DateTimeOffset expires) =>
        MakeTokenWithStamp(signingKey, issuer, audience, expires, stamp: "x");

    private static string MakeTokenWithStamp(
        string signingKey,
        string issuer,
        string audience,
        DateTimeOffset expires,
        string stamp)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("security_stamp", stamp)],
            notBefore: expires.UtcDateTime.AddMinutes(-30),
            expires: expires.UtcDateTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string MakeTokenWithoutStamp(
        string signingKey,
        string issuer,
        string audience,
        DateTimeOffset expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore: expires.UtcDateTime.AddMinutes(-30),
            expires: expires.UtcDateTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
