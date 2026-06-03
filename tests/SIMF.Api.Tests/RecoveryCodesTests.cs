using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the single-use recovery codes that act as a fallback
/// for TOTP (decision D-040). Every test enrols a fresh visitor in TOTP so the
/// tests don't interfere with each other.
/// </summary>
public sealed class RecoveryCodesTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RecoveryCodesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Confirm_returns_ten_unique_well_formed_codes()
    {
        var enrolled = await EnrolAsync();

        Assert.Equal(10, enrolled.Codes.Count);
        Assert.All(enrolled.Codes,
            code => Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", code));
        Assert.Equal(10, enrolled.Codes.Distinct().Count());
    }

    [Fact]
    public async Task Profile_reports_the_remaining_count()
    {
        var enrolled = await EnrolAsync();

        var fresh = await RefreshAsync(enrolled.RefreshToken);
        var profile = await ReadProfileAsync(fresh.AccessToken);
        Assert.Equal(10, profile.RecoveryCodesRemaining);

        // Consume one code via the sign-in fallback.
        await SignInWithRecoveryCodeAsync(enrolled.Email, enrolled.Codes[0]);

        // Re-check via another refresh + profile read. (The previous fresh
        // token's stamp may be moved by Identity housekeeping; just refresh.)
        var second = await RefreshAsync(fresh.RefreshToken);
        var refreshed = await ReadProfileAsync(second.AccessToken);
        Assert.Equal(9, refreshed.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task A_recovery_code_signs_the_user_in_in_place_of_TOTP()
    {
        var enrolled = await EnrolAsync();

        var tokens = await SignInWithRecoveryCodeAsync(enrolled.Email, enrolled.Codes[0]);

        Assert.NotEmpty(tokens.AccessToken);
        Assert.Equal(enrolled.Email, tokens.User.Email);
    }

    [Fact]
    public async Task The_same_code_can_be_used_only_once()
    {
        var enrolled = await EnrolAsync();
        await SignInWithRecoveryCodeAsync(enrolled.Email, enrolled.Codes[0]);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = enrolled.Email, Password = AuthFlow.Password });
        var challenge =
            (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-recovery-code",
            new VerifyRecoveryCodeRequest
            {
                MfaToken = challenge.MfaToken!,
                Code = enrolled.Codes[0],
            });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = (await verify.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthRecoveryCodeInvalid, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));
    }

    [Fact]
    public async Task A_wrong_code_is_rejected()
    {
        var enrolled = await EnrolAsync();

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = enrolled.Email, Password = AuthFlow.Password });
        var challenge =
            (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-recovery-code",
            new VerifyRecoveryCodeRequest
            {
                MfaToken = challenge.MfaToken!,
                Code = "AAAAA-AAAAA",
            });

        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        var body = (await verify.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthRecoveryCodeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Regenerate_invalidates_the_old_batch()
    {
        var enrolled = await EnrolAsync();
        var fresh = await RefreshAsync(enrolled.RefreshToken);

        var regen = await PostAuthAsync<object>(
            "/api/v1/app/account/recovery-codes/regenerate", null, fresh.AccessToken);
        var second = (await regen.Content.ReadFromJsonAsync<ApiResult<RecoveryCodesResponse>>())!;
        Assert.Equal(10, second.Data!.RecoveryCodes.Count);
        Assert.DoesNotContain(enrolled.Codes[0], second.Data.RecoveryCodes);

        // The first batch is now dead — using one of its codes is rejected.
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = enrolled.Email, Password = AuthFlow.Password });
        var challenge =
            (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-recovery-code",
            new VerifyRecoveryCodeRequest
            {
                MfaToken = challenge.MfaToken!,
                Code = enrolled.Codes[0],
            });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
    }

    [Fact]
    public async Task Disable_wipes_every_recovery_code()
    {
        var enrolled = await EnrolAsync();
        var fresh = await RefreshAsync(enrolled.RefreshToken);

        // Disable using the active TOTP secret — wait one full window for the
        // replay guard.
        await Task.Delay(TimeSpan.FromSeconds(31));
        var disableCode = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(enrolled.Secret))
            .ComputeTotp(DateTime.UtcNow);
        var disable = await PostAuthAsync(
            "/api/v1/app/auth/totp/disable",
            new TotpDisableRequest { Code = disableCode },
            fresh.AccessToken);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = db.Users.Single(u => u.Email == enrolled.Email);
        var remaining = await db.TotpRecoveryCodes.CountAsync(c => c.CreateBy == user.Id);
        Assert.Equal(0, remaining);
    }

    // -- helpers --------------------------------------------------------------

    private sealed record EnrolledUser(
        string Email,
        string AccessToken,
        string RefreshToken,
        string Secret,
        IReadOnlyList<string> Codes);

    /// <summary>
    /// Registers a fresh visitor, signs them in (no 2FA yet), enrols them in
    /// TOTP and returns the issued recovery codes.
    /// </summary>
    private async Task<EnrolledUser> EnrolAsync()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var setupResponse = await PostAuthAsync<object>(
            "/api/v1/app/auth/totp/setup", null, tokens.AccessToken);
        var setup = (await setupResponse.Content.ReadFromJsonAsync<ApiResult<TotpSetupResponse>>())!
            .Data!;

        var firstCode = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(setup.Secret))
            .ComputeTotp(DateTime.UtcNow);
        var confirm = await PostAuthAsync(
            "/api/v1/app/auth/totp/confirm",
            new TotpConfirmRequest { Code = firstCode },
            tokens.AccessToken);
        var confirmBody =
            (await confirm.Content.ReadFromJsonAsync<ApiResult<TotpConfirmResponse>>())!;
        return new EnrolledUser(
            tokens.User.Email,
            tokens.AccessToken,
            tokens.RefreshToken,
            setup.Secret,
            confirmBody.Data!.RecoveryCodes);
    }

    /// <summary>Sign in fully via the recovery-code path; returns the issued tokens.</summary>
    private async Task<AuthTokens> SignInWithRecoveryCodeAsync(string email, string code)
    {
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var challenge =
            (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!;
        var verify = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-recovery-code",
            new VerifyRecoveryCodeRequest { MfaToken = challenge.MfaToken!, Code = code });
        verify.EnsureSuccessStatusCode();
        var body = (await verify.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!;
        return body.Data!;
    }

    private async Task<AuthTokens> RefreshAsync(string refreshToken)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/refresh", new RefreshRequest { RefreshToken = refreshToken });
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!;
        return body.Data!;
    }

    private async Task<ProfileResponse> ReadProfileAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/app/account/profile");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<ProfileResponse>>())!;
        return body.Data!;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }
}
