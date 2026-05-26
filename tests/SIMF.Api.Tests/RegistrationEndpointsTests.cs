using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the account-creation endpoints — sign-up, verify-email
/// and resend-code (SIMF-API-001 section 12.4).
/// </summary>
public sealed class RegistrationEndpointsTests : IClassFixture<SimfApiFactory>
{
    private const string ValidPassword = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RegistrationEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@simf.test";

    private Task<HttpResponseMessage> SignUpAsync(string email, string? password = null) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = password ?? ValidPassword,
                ConfirmPassword = password ?? ValidPassword,
            });

    private Task<HttpResponseMessage> VerifyEmailAsync(string email, string code) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = code });

    // -- sign-up --------------------------------------------------------------

    [Fact]
    public async Task SignUp_returns_201_for_a_new_account()
    {
        var email = NewEmail();

        var response = await SignUpAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignUpResponse>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal(email, body.Data!.Email);
        Assert.Equal(600, body.Data.CodeExpiresInSeconds);
    }

    [Fact]
    public async Task SignUp_returns_409_for_a_duplicate_email()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await SignUpAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, body!.Error!.Code);
    }

    [Fact]
    public async Task SignUp_returns_400_for_a_weak_password()
    {
        var response = await SignUpAsync(NewEmail(), password: "weak");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Error!.Code);
    }

    [Fact]
    public async Task SignUp_returns_400_when_the_passwords_do_not_match()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest
            {
                Email = NewEmail(),
                Password = ValidPassword,
                ConfirmPassword = "Different1!",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.ValidationFailed, body!.Error!.Code);
    }

    // -- verify-email ---------------------------------------------------------

    [Fact]
    public async Task VerifyEmail_returns_200_for_the_correct_code()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await VerifyEmailAsync(email, GetActiveCode(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<VerifyEmailResponse>>();
        Assert.True(body!.Data!.EmailVerified);
        Assert.Equal(AccountState.EmailVerified, GetAccountState(email));
    }

    [Fact]
    public async Task VerifyEmail_returns_400_for_a_wrong_code()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        var response = await VerifyEmailAsync(email, WrongCodeFor(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task VerifyEmail_returns_404_for_an_unknown_email()
    {
        var response = await VerifyEmailAsync(NewEmail(), "123456");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_returns_AUTH_CODE_EXPIRED_after_the_code_lifetime()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        var code = GetActiveCode(email);

        _factory.Time.Advance(TimeSpan.FromMinutes(11));
        var response = await VerifyEmailAsync(email, code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthCodeExpired, body!.Error!.Code);
    }

    [Fact]
    public async Task VerifyEmail_blocks_further_attempts_after_five_wrong_codes()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        var correctCode = GetActiveCode(email);
        var wrongCode = WrongCodeFor(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await VerifyEmailAsync(email, wrongCode);
        }

        // Even the correct code is now rejected — the code is locked.
        var response = await VerifyEmailAsync(email, correctCode);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task VerifyEmail_on_an_already_verified_account_is_rejected()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await VerifyEmailAsync(email, GetActiveCode(email));

        var response = await VerifyEmailAsync(email, "123456");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body!.Error!.Code);
    }

    // -- resend-code ----------------------------------------------------------

    [Fact]
    public async Task ResendCode_invalidates_the_previous_code()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        var firstCode = GetActiveCode(email);

        var resend = await _client.PostAsJsonAsync(
            "/api/v1/auth/resend-code",
            new ResendCodeRequest { Email = email });
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var oldCodeResponse = await VerifyEmailAsync(email, firstCode);
        Assert.Equal(HttpStatusCode.BadRequest, oldCodeResponse.StatusCode);
    }

    [Fact]
    public async Task ResendCode_then_verify_with_the_new_code_succeeds()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        await _client.PostAsJsonAsync(
            "/api/v1/auth/resend-code",
            new ResendCodeRequest { Email = email });

        var response = await VerifyEmailAsync(email, GetActiveCode(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendCode_returns_404_for_an_unknown_email()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/resend-code",
            new ResendCodeRequest { Email = NewEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendCode_returns_429_once_the_per_account_cap_is_reached()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            lastResponse = await _client.PostAsJsonAsync(
                "/api/v1/auth/resend-code",
                new ResendCodeRequest { Email = email });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        var body = await lastResponse.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.RateLimitExceeded, body!.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private string GetActiveCode(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == AccountCodePurpose.EmailVerification
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .Code;
    }

    /// <summary>A six-digit code guaranteed to differ from the account's active code.</summary>
    private string WrongCodeFor(string email) =>
        GetActiveCode(email) == "000000" ? "999999" : "000000";

    private AccountState GetAccountState(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return database.Users.Single(candidate => candidate.Email == email).AccountState;
    }
}
