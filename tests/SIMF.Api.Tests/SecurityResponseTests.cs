// Locks the NCA Wave 1 + Wave 4 response hardening: baseline security headers
// on every response (A3-4/A5-12/A5-13), a strict CSP on the JSON API, and the
// ApiResult envelope on a 403 (A1-12).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SecurityResponseTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SecurityResponseTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Every_response_carries_the_baseline_security_headers()
    {
        // Any response runs through SecurityHeadersMiddleware — use an
        // unauthenticated request that the framework answers without a body.
        var response = await _client.GetAsync("/api/v1/this-route-does-not-exist");

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        // A3-4 — the JSON API ships a strict, enforced CSP (non-Swagger path).
        Assert.Contains("default-src 'none'", Single(response, "Content-Security-Policy"));
        Assert.Contains("frame-ancestors 'none'", Single(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task An_error_envelope_still_carries_the_baseline_security_headers()
    {
        // Regression: SecurityHeadersMiddleware used to set its headers before
        // calling next(), and ErrorHandlingMiddleware - which runs inside it -
        // calls Response.Clear() to write a failure envelope. Every one of the
        // ~770 ApiException responses therefore shipped with no security
        // headers at all, while the 404-route test above kept passing because
        // routing answers that one without ever clearing the response.
        var tokens = await AuthFlow.SignInApprovedVisitorWithoutTwoFactorAsync(_client, _factory);

        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/app/sessions/{Guid.NewGuid()}/seats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        // Proves the response really came from ErrorHandlingMiddleware.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.SessionNotFound, body!.Error!.Code);

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Contains("default-src 'none'", Single(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task Json_responses_declare_an_explicit_utf8_charset()
    {
        // A protected route hit without a token returns the ApiResult envelope as
        // JSON via the bearer challenge (401) — assert the explicit charset (A5-12).
        var response = await _client.PostAsJsonAsync("/api/v1/admin/sessions/list", new GridQuery());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var contentType = response.Content.Headers.ContentType;
        Assert.NotNull(contentType);
        Assert.Equal("application/json", contentType!.MediaType);
        Assert.Equal("utf-8", contentType.CharSet);
    }

    [Fact]
    public async Task A_forbidden_request_returns_the_ApiResult_envelope()
    {
        // A1-12 — an authenticated visitor (no admin permission) hitting an admin
        // endpoint is 403'd with the standard envelope (not an empty body).
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/sessions/list")
        {
            Content = JsonContent.Create(new GridQuery()),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.Forbidden, body.Error!.Code);
    }

    private static string Single(HttpResponseMessage response, string header)
    {
        Assert.True(
            response.Headers.TryGetValues(header, out var values),
            $"Expected header '{header}' on the response.");
        return string.Join(",", values!);
    }
}
