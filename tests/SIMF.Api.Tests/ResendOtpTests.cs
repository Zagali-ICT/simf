// #12 — re-issue the emailed sign-in OTP for an in-progress 2FA session, so the
// OTP screen's "Resend" works in place instead of bouncing back to sign-in.
using System.Net;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ResendOtpTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ResendOtpTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Resend_reissues_a_code_that_verifies_against_the_same_ticket()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        var otpToken = await SignInForOtpTokenAsync(email);
        // The original emailed code (the resend will consume + replace it).
        var firstCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.SignInOtp);

        var resend = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resend-otp", new ResendOtpRequest { OtpToken = otpToken });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var body = (await resend.Content
            .ReadFromJsonAsync<ApiResult<ResendOtpResponse>>())!.Data!;
        Assert.Equal(60, body.CooldownSeconds);

        // The freshly issued code verifies against the SAME (unchanged) ticket.
        var newCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.SignInOtp);
        Assert.NotEqual(firstCode, newCode);
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-otp",
            new VerifyOtpRequest { OtpToken = otpToken, Code = newCode });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content
            .ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
    }

    [Fact]
    public async Task Resend_consumes_the_previous_code_so_the_old_one_no_longer_verifies()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        var otpToken = await SignInForOtpTokenAsync(email);
        var oldCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.SignInOtp);

        var resend = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resend-otp", new ResendOtpRequest { OtpToken = otpToken });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        // The old code was consumed when the new one was issued → it must fail.
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-otp",
            new VerifyOtpRequest { OtpToken = otpToken, Code = oldCode });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    [Fact]
    public async Task Resend_with_an_unknown_ticket_is_rejected()
    {
        var resend = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resend-otp",
            new ResendOtpRequest { OtpToken = "not-a-real-ticket" });
        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
        var body = (await resend.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthOtpTokenInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Resend_is_capped_per_window()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        var otpToken = await SignInForOtpTokenAsync(email);

        // Sign-in issued code #1; the budget is 5/window, so 4 resends succeed…
        for (var i = 0; i < 4; i++)
        {
            var ok = await _client.PostAsJsonAsync(
                "/api/v1/app/auth/resend-otp", new ResendOtpRequest { OtpToken = otpToken });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        // …and the 5th resend (the 6th code request) is throttled.
        var throttled = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/resend-otp", new ResendOtpRequest { OtpToken = otpToken });
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        var body = (await throttled.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RateLimitExceeded, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<string> SignInForOtpTokenAsync(string email)
    {
        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var challenge = (await signIn.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        Assert.False(string.IsNullOrEmpty(challenge.OtpToken));
        return challenge.OtpToken!;
    }
}
