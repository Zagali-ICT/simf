// D-172 (gap doc G10, PDF §2.5) — Face ID / Touch ID biometric sign-in
// via the ECDSA P-256 device-key ceremony.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DeviceKeySignInTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DeviceKeySignInTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Full_ceremony_round_trip_succeeds()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();

        // 1) Generate keypair, export public key (SubjectPublicKeyInfo).
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(
            ecdsa.ExportSubjectPublicKeyInfo());

        // 2) Register the public key (authenticated).
        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64,
                Algorithm = "ES256",
                Label = "iPhone 15 Pro",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        // 3) Ask for a challenge (anonymous).
        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyChallenge>>())!.Data!;

        // 4) Sign the challenge bytes and submit.
        var signature = ecdsa.SignData(
            Convert.FromBase64String(challenge.Challenge),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var signInResponse = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(signature),
            });
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        var tokens = (await signInResponse.Content
            .ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task Wrong_signature_returns_401()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64, Algorithm = "ES256", Label = "Test",
            }, visitor);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyChallenge>>())!.Data!;

        // Sign DIFFERENT data than the challenge — verification must fail.
        var badSignature = ecdsa.SignData(
            new byte[] { 1, 2, 3, 4 },
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var signInResponse = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(badSignature),
            });
        Assert.Equal(HttpStatusCode.Unauthorized, signInResponse.StatusCode);
    }

    [Fact]
    public async Task Replay_of_consumed_challenge_returns_401()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64, Algorithm = "ES256", Label = "Test",
            }, visitor);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyChallenge>>())!.Data!;

        var signature = ecdsa.SignData(
            Convert.FromBase64String(challenge.Challenge),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        // First sign-in succeeds.
        var first = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(signature),
            });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replay with the same challenge + signature → 401.
        var replay = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(signature),
            });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Revoked_key_cannot_be_used_for_challenge()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64, Algorithm = "ES256", Label = "Test",
            }, visitor);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        var revoke = await DeleteAuthAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}", visitor);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, challengeResponse.StatusCode);
        var body = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyRevoked, body.Error!.Code);
    }

    [Fact]
    public async Task User_cannot_revoke_someone_elses_device_key()
    {
        var (visitorA, _) = await CreateApprovedVisitorAsync();
        var (visitorB, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64, Algorithm = "ES256", Label = "A's iPhone",
            }, visitorA);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        // B tries to delete A's key — must return 404 (the service hides
        // the row from non-owners).
        var revoke = await DeleteAuthAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}", visitorB);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }

    [Fact]
    public async Task Register_with_bad_base64_public_key_is_DEVICE_KEY_INVALID()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = "not-base64!!!", Algorithm = "ES256", Label = "Bad",
            }, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Register_with_unsupported_algorithm_is_DEVICE_KEY_ALGORITHM_UNSUPPORTED()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = publicKeyBase64, Algorithm = "RS256", Label = "Test",
            }, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyAlgorithmUnsupported, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
