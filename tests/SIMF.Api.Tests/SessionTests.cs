using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the session lifecycle — refresh-token rotation and
/// reuse detection, sign-out, and the bearer authentication middleware
/// (SIMF-API-001 section 12.4).
/// </summary>
public sealed class SessionTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_rotates_the_refresh_token()
    {
        var tokens = await SignInVisitorAsync();

        var response = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = (await response.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_returns_401()
    {
        var response = await RefreshAsync("not-a-real-refresh-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_family()
    {
        var tokens = await SignInVisitorAsync();

        // Rotate once — the original token is now revoked, the rotated one live.
        var rotated = (await (await RefreshAsync(tokens.RefreshToken)).Content
            .ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;

        // Present the original (revoked) token again — that is reuse.
        var reuse = await RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The family is revoked: even the legitimate rotated token now fails.
        var familyRevoked = await RefreshAsync(rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);
    }

    [Fact]
    public async Task Sign_out_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/v1/auth/sign-out", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_with_a_valid_access_token_succeeds()
    {
        var tokens = await SignInVisitorAsync();

        var response = await SignOutAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_revokes_the_refresh_token()
    {
        var tokens = await SignInVisitorAsync();
        await SignOutAsync(tokens.AccessToken);

        var refresh = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task An_access_token_is_rejected_after_sign_out()
    {
        var tokens = await SignInVisitorAsync();
        await SignOutAsync(tokens.AccessToken);

        // The same access token, reused after sign-out — the security stamp moved.
        var response = await SignOutAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest { RefreshToken = refreshToken });

    private Task<HttpResponseMessage> SignOutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private async Task<AuthTokens> SignInVisitorAsync()
    {
        var email = $"session-{Guid.NewGuid():N}@simf.test";

        await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = Password, ConfirmPassword = Password });
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = GetActiveCode(email, AccountCodePurpose.EmailVerification),
            });

        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = Password });
        var challenge = (await signIn.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;

        var verify = await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-otp",
            new VerifyOtpRequest
            {
                OtpToken = challenge.OtpToken!,
                Code = GetActiveCode(email, AccountCodePurpose.SignInOtp),
            });
        return (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
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
