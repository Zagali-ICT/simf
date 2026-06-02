using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the TOTP enrolment endpoints (myComment item #11).
/// </summary>
public sealed class TotpEnrolmentTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public TotpEnrolmentTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Setup_returns_a_secret_an_otpauth_uri_and_a_qr_svg()
    {
        var (token, _, _) = await SignInAsync();
        var response = await PostAuthAsync<object>("/api/v1/app/auth/totp/setup", null, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!;
        Assert.True(body.Success);
        Assert.NotEmpty(body.Data!.Secret);
        Assert.StartsWith("otpauth://totp/", body.Data.OtpAuthUri);
        Assert.Contains("<svg", body.Data.QrCodeSvg);
    }

    [Fact]
    public async Task Confirm_with_a_valid_code_enables_two_factor()
    {
        var (token, email, _) = await SignInAsync();
        var setup = await ReadAsync<TotpSetupResponse>(
            await PostAuthAsync<object>("/api/v1/app/auth/totp/setup", null, token));

        var bytes = Base32Encoding.ToBytes(setup.Secret);
        var code = new Totp(bytes).ComputeTotp(DateTime.UtcNow);

        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = code }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<TotpConfirmResponse>>())!;
        Assert.True(body.Success);
        Assert.True(body.Data!.TwoFactorEnabled);
        // D-040: confirm returns 10 single-use recovery codes plaintext once.
        Assert.Equal(10, body.Data.RecoveryCodes.Count);
        Assert.All(body.Data.RecoveryCodes, code => Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", code));
        Assert.Equal(10, body.Data.RecoveryCodes.Distinct().Count());

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task Confirm_with_a_wrong_code_returns_400()
    {
        var (token, _, _) = await SignInAsync();
        await PostAuthAsync<object>("/api/v1/app/auth/totp/setup", null, token);

        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = "000000" }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.TotpEnrolmentCodeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Confirm_without_setup_returns_400()
    {
        var (token, _, _) = await SignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = "123456" }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.TotpEnrolmentNotStarted, body.Error!.Code);
    }

    [Fact]
    public async Task Disable_with_a_valid_code_turns_two_factor_off()
    {
        var (token, email, refreshToken) = await SignInAsync();
        var setup = await ReadAsync<TotpSetupResponse>(
            await PostAuthAsync<object>("/api/v1/app/auth/totp/setup", null, token));

        var bytes = Base32Encoding.ToBytes(setup.Secret);
        var enableCode = new Totp(bytes).ComputeTotp(DateTime.UtcNow);
        await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = enableCode }, token);

        // Confirming TOTP rolls the security stamp; the old access token is
        // now rejected. Rotate to a fresh pair via the refresh endpoint.
        var refreshed = await ReadAsync<AuthTokens>(await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }));

        // Wait one full step so the disable code is a different time-step
        // than the one used by Confirm — otherwise the replay guard rejects.
        await Task.Delay(TimeSpan.FromSeconds(31));
        var disableCode = new Totp(bytes).ComputeTotp(DateTime.UtcNow);
        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/disable",
            new TotpDisableRequest { Code = disableCode },
            refreshed.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.False(await users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task Disable_without_two_factor_enabled_returns_400()
    {
        var (token, _, _) = await SignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/disable", new TotpDisableRequest { Code = "123456" }, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.TotpNotEnabled, body.Error!.Code);
    }

    [Fact]
    public async Task Setup_without_a_bearer_token_returns_401()
    {
        var response = await _client.PostAsync("/api/v1/app/auth/totp/setup", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Every_failure_carries_a_non_empty_Arabic_message()
    {
        // Pins D-030: every ApiException flowing out of these endpoints
        // populates MessageArabic. The cheapest failure path —
        // confirm-without-setup — exercises both languages.
        var (token, _, _) = await SignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = "123456" }, token);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.False(string.IsNullOrWhiteSpace(body.Error!.Message));
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));
    }

    [Fact]
    public async Task Pairing_returns_404_when_the_account_has_no_active_secret()
    {
        // D-096: a fresh visitor has not enrolled in TOTP, so the pairing
        // endpoint returns 404 — the CP page surfaces this as "use the
        // Profile page to enrol" rather than as an error.
        var (token, _, _) = await SignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/auth/totp/pairing");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pairing_returns_the_same_QR_for_the_enrolled_users_active_secret()
    {
        // D-096: an enrolled user can re-fetch the QR for their existing secret
        // without rotating it; the response carries the SAME secret each call
        // (the read endpoint is idempotent), unlike POST /auth/totp/setup which
        // rotates a candidate secret on every call.
        var (token, _, refreshToken) = await SignInAsync();
        var setup = await ReadAsync<TotpSetupResponse>(
            await PostAuthAsync<object>("/api/v1/app/auth/totp/setup", null, token));
        var bytes = Base32Encoding.ToBytes(setup.Secret);
        var code = new Totp(bytes).ComputeTotp(DateTime.UtcNow);
        var confirm = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm", new TotpConfirmRequest { Code = code }, token);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // Confirming TOTP rolls the security stamp; the old access token is
        // now rejected. Rotate to a fresh pair via the refresh endpoint.
        var refreshed = await ReadAsync<AuthTokens>(await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/auth/totp/pairing");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!;
        Assert.True(body.Success);
        Assert.Equal(setup.Secret, body.Data!.Secret);
        Assert.StartsWith("otpauth://totp/", body.Data.OtpAuthUri);
        Assert.Contains("<svg", body.Data.QrCodeSvg);
    }

    private async Task<(string AccessToken, string Email, string RefreshToken)> SignInAsync()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        return (tokens.AccessToken, tokens.User.Email, tokens.RefreshToken);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token)
        where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var envelope = (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!;
        return envelope.Data!;
    }
}
