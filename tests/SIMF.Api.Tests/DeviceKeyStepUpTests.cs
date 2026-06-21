// #7a — emailed-OTP step-up guarding biometric (Face-ID) device-key
// enrolment. Exercises the real gate (DeviceKey:RequireStepUpForEnrol ON):
// register is rejected without a fresh code, the send endpoint issues one,
// a valid code enrols + is single-use, a wrong code is rejected, and the
// re-issue is capped per window.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Runs the API with <c>DeviceKey:RequireStepUpForEnrol</c> ON so the
/// real enrolment gate is exercised end-to-end (the base factory leaves it OFF
/// for the device-key ceremony tests).</summary>
public sealed class BiometricStepUpApiFactory : SimfApiFactory
{
    public BiometricStepUpApiFactory() =>
        Environment.SetEnvironmentVariable("DeviceKey__RequireStepUpForEnrol", "true");
}

public sealed class DeviceKeyStepUpTests : IClassFixture<BiometricStepUpApiFactory>
{
    private readonly BiometricStepUpApiFactory _factory;
    private readonly HttpClient _client;

    public DeviceKeyStepUpTests(BiometricStepUpApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_without_step_up_code_is_rejected_when_gate_on()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys", NewRegisterRequest(stepUpCode: null), token);

        Assert.Equal(HttpStatusCode.Unauthorized, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BiometricStepUpRequired, body.Error!.Code);
    }

    [Fact]
    public async Task Send_step_up_returns_masked_email_and_issues_a_code()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();

        var send = await PostAuthAsync(
            "/api/v1/app/auth/device-keys/step-up", new { }, token);

        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var result = (await send.Content
            .ReadFromJsonAsync<ApiResult<SendBiometricStepUpResponse>>())!.Data!;
        // dk-<guid>@simf.test → "d***@simf.test" (first char + full domain).
        Assert.StartsWith("d***@", result.MaskedEmail);
        Assert.EndsWith("@simf.test", result.MaskedEmail);
        Assert.Equal(600, result.ExpiresInSeconds);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await db.AccountCodes.CountAsync(c =>
            c.UserId == userId
            && c.Purpose == AccountCodePurpose.BiometricEnrolStepUp);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Register_with_valid_code_succeeds_then_code_is_single_use()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        await SeedStepUpCodeAsync(userId, "123456");

        var ok = await PostAuthAsync(
            "/api/v1/app/auth/device-keys", NewRegisterRequest(stepUpCode: "123456"), token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // The code was consumed on success — re-using it is rejected.
        var reuse = await PostAuthAsync(
            "/api/v1/app/auth/device-keys", NewRegisterRequest(stepUpCode: "123456"), token);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Register_with_wrong_code_is_BIOMETRIC_STEP_UP_INVALID()
    {
        var (token, userId) = await CreateApprovedVisitorAsync();
        await SeedStepUpCodeAsync(userId, "123456");

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys", NewRegisterRequest(stepUpCode: "999999"), token);

        Assert.Equal(HttpStatusCode.Unauthorized, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BiometricStepUpInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Step_up_send_is_capped_per_window()
    {
        var (token, _) = await CreateApprovedVisitorAsync();

        for (var i = 0; i < 5; i++)
        {
            var ok = await PostAuthAsync(
                "/api/v1/app/auth/device-keys/step-up", new { }, token);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var sixth = await PostAuthAsync(
            "/api/v1/app/auth/device-keys/step-up", new { }, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        var body = (await sixth.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RateLimitExceeded, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private static RegisterDeviceKeyRequest NewRegisterRequest(string? stepUpCode)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new RegisterDeviceKeyRequest
        {
            PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            Algorithm = "ES256",
            Label = "Test device",
            StepUpCode = stepUpCode,
        };
    }

    /// <summary>Inserts a real step-up <see cref="AccountCode"/> row whose hash
    /// matches <paramref name="plaintext"/>. The keyed hasher is a process-wide
    /// static seeded from the same test signing key the host configured, so the
    /// hash computed here equals the one the service compares against.</summary>
    private async Task SeedStepUpCodeAsync(Guid userId, string plaintext)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        db.AccountCodes.Add(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = AccountCodePurpose.BiometricEnrolStepUp,
            Code = AccountCodeHasher.Hash(plaintext),
            CreatedAt = _factory.Time.GetUtcNow(),
            ExpiresAt = _factory.Time.GetUtcNow().AddMinutes(10),
        });
        await db.SaveChangesAsync();
    }

    private async Task<(string accessToken, Guid userId)> CreateApprovedVisitorAsync()
    {
        var email = $"dk-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "DK Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (envelope.Data!.Tokens!.AccessToken, userId);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
