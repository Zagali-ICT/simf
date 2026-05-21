using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for sign-in and the two second factors (SIMF-API-001
/// section 12.4).
/// </summary>
public sealed class SignInTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"signin-{Guid.NewGuid():N}@simf.test";

    [Fact]
    public async Task SignIn_with_an_unknown_email_returns_401()
    {
        var response = await SignInAsync(NewEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, body!.Error!.Code);
    }

    [Fact]
    public async Task SignIn_with_a_wrong_password_returns_401()
    {
        var email = await RegisterVerifiedVisitorAsync();

        var response = await SignInAsync(email, "Wrong1!Password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_before_email_verification_returns_403()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await SignInAsync(email, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthEmailNotVerified, body!.Error!.Code);
    }

    [Fact]
    public async Task A_visitor_signs_in_and_completes_with_the_emailed_code()
    {
        var email = await RegisterVerifiedVisitorAsync();

        var challenge = await ExpectChallengeAsync(email, Password);
        Assert.True(challenge.MfaRequired);
        Assert.NotNull(challenge.OtpToken);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = GetActiveCode(email, AccountCodePurpose.SignInOtp),
            });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal(email, tokens.User.Email);
    }

    [Fact]
    public async Task Verify_otp_with_a_wrong_code_returns_400()
    {
        var email = await RegisterVerifiedVisitorAsync();
        var challenge = await ExpectChallengeAsync(email, Password);
        var realCode = GetActiveCode(email, AccountCodePurpose.SignInOtp);

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = realCode == "000000" ? "999999" : "000000",
            });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthOtpInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task An_administrator_signs_in_and_completes_with_a_TOTP_code()
    {
        await SeedSuperAdminAsync();

        var challenge = await ExpectChallengeAsync("superadmin@simf.test", "ChangeMe!Test1");
        Assert.NotNull(challenge.MfaToken);

        var totp = new Totp(Base32Encoding.ToBytes("JBSWY3DPEHPK3PXP")).ComputeTotp();
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.MfaToken!, Code = totp });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }

    [Fact]
    public async Task Verify_totp_with_a_wrong_code_returns_400()
    {
        await SeedSuperAdminAsync();
        var challenge = await ExpectChallengeAsync("superadmin@simf.test", "ChangeMe!Test1");

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-totp",
            new VerifyTotpRequest { MfaToken = challenge.MfaToken!, Code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = await verify.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthTotpInvalid, body!.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = password });

    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });

    private async Task<SignInResponse> ExpectChallengeAsync(string email, string password)
    {
        var response = await SignInAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
    }

    private async Task<string> RegisterVerifiedVisitorAsync()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });
        return email;
    }

    private async Task SeedSuperAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    }

    private string GetActiveCode(string email, AccountCodePurpose purpose)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
    }
}
