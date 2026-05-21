using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for password recovery — forgot-password, reset-password
/// and change-password (SIMF-API-001 section 12.7).
/// </summary>
public sealed class PasswordTests : IClassFixture<SimfApiFactory>
{
    private const string Password = AuthFlow.Password;
    private const string NewPassword = "NewPassw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Forgot_password_gives_the_same_response_whether_or_not_the_account_exists()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        var knownBody = (await (await ForgotAsync(email)).Content
            .ReadFromJsonAsync<ApiResult<ForgotPasswordResponse>>())!;
        var unknownBody = (await (await ForgotAsync($"nobody-{Guid.NewGuid():N}@simf.test")).Content
            .ReadFromJsonAsync<ApiResult<ForgotPasswordResponse>>())!;

        Assert.True(knownBody.Success);
        Assert.True(unknownBody.Success);
        Assert.Equal(
            knownBody.Data!.CodeExpiresInSeconds,
            unknownBody.Data!.CodeExpiresInSeconds);
    }

    [Fact]
    public async Task Forgot_password_for_a_disabled_account_issues_no_code()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        AuthFlow.SetAccountState(_factory, email, AccountState.Disabled);

        var response = await ForgotAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, AuthFlow.CodeCount(_factory, email, AccountCodePurpose.PasswordReset));
    }

    [Fact]
    public async Task Forgot_password_stops_issuing_codes_after_the_per_account_cap()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);

        for (var attempt = 0; attempt < 7; attempt++)
        {
            await ForgotAsync(email);
        }

        // The cap is five per hour — the sixth and seventh requests issue nothing.
        Assert.Equal(5, AuthFlow.CodeCount(_factory, email, AccountCodePurpose.PasswordReset));
    }

    [Fact]
    public async Task Reset_password_with_a_valid_code_sets_the_new_password()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await ForgotAsync(email);
        var code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);

        var reset = await ResetAsync(email, code, NewPassword);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(email, Password)).StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_a_wrong_code_returns_400()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await ForgotAsync(email);
        var realCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);

        var reset = await ResetAsync(email, realCode == "000000" ? "999999" : "000000", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var body = await reset.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthResetCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Reset_password_for_an_unknown_email_returns_400()
    {
        var reset = await ResetAsync($"nobody-{Guid.NewGuid():N}@simf.test", "123456", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var body = await reset.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthResetCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Reset_password_is_rejected_after_five_wrong_attempts()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await ForgotAsync(email);
        var realCode = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);
        var wrongCode = realCode == "000000" ? "999999" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await ResetAsync(email, wrongCode, NewPassword);
        }

        // The code is now attempt-capped — even the correct code is refused.
        var capped = await ResetAsync(email, realCode, NewPassword);
        Assert.Equal(HttpStatusCode.BadRequest, capped.StatusCode);
    }

    [Fact]
    public async Task Reset_password_revokes_existing_refresh_tokens()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);
        await ForgotAsync(tokens.User.Email);
        var code = AuthFlow.GetActiveCode(
            _factory, tokens.User.Email, AccountCodePurpose.PasswordReset);
        await ResetAsync(tokens.User.Email, code, NewPassword);

        var refresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task A_completed_password_reset_writes_an_audit_entry()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await ForgotAsync(email);
        var code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset);

        await ResetAsync(email, code, NewPassword);

        Assert.True(AuthFlow.AuditEntryExists(_factory, email, AuditEvents.PasswordResetCompleted));
    }

    [Fact]
    public async Task Change_password_requires_authentication()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = Password,
                NewPassword = NewPassword,
                ConfirmPassword = NewPassword,
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_the_correct_current_password_succeeds()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        var change = await ChangeAsync(tokens.AccessToken, Password, NewPassword);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // The new password works at sign-in.
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(tokens.User.Email, NewPassword)).StatusCode);
        // The access token used for the change is now stale (the stamp moved).
        var stale = await ChangeAsync(tokens.AccessToken, NewPassword, "Another1!Pwd");
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_a_wrong_current_password_returns_400()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        var change = await ChangeAsync(tokens.AccessToken, "Wrong1!Password", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        var body = await change.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, body!.Error!.Code);
    }

    [Fact]
    public async Task Change_password_revokes_existing_refresh_tokens()
    {
        var tokens = await AuthFlow.SignInVisitorAsync(_client, _factory);

        await ChangeAsync(tokens.AccessToken, Password, NewPassword);

        var refresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

    private Task<HttpResponseMessage> ResetAsync(string email, string code, string newPassword) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            });

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = password });

    private Task<HttpResponseMessage> ChangeAsync(
        string accessToken, string currentPassword, string newPassword)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmPassword = newPassword,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
