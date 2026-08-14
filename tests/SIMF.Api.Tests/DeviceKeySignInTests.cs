// D-172 (gap doc G10, PDF §2.5) — Face ID / Touch ID biometric sign-in
// via the ECDSA P-256 device-key ceremony.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
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

    // Interop golden vector — a real ECDSA P-256 public key + signature
    // produced by the Flutter app's pointycastle DeviceKeyClient (Dart),
    // captured once from the client and pinned here. The other ceremony tests
    // generate the key AND sign with .NET's own ECDsa, so they never exercise
    // the .NET <-> Dart byte interop. This one proves the app's hand-built
    // SubjectPublicKeyInfo encoding imports, and its IEEE-P1363 (r||s)
    // signature over SHA-256(challenge) verifies, in .NET's ECDsa verify path.
    //
    // Regenerate (only if DeviceKeyClient's encoding ever changes): run a
    // one-off Dart test that calls DeviceKeyClient.generateKeyPair() and
    // .sign(challengeBase64: <the fixed 32-byte challenge below>) and paste the
    // three emitted base64 values here.
    private const string DartClientPublicKeySpkiBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEubfCzZbRNptyv7Gchdc927b+8z6iwi/ieYm/TipSMsg+eXRmufogaD/8E7gxSDMiNMypwHdwawLqBbkl6CSUjA==";

    // base64 of the 32 bytes 0x00..0x1f.
    private const string DartClientChallengeBase64 =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private const string DartClientSignatureBase64 =
        "RDmeI9xHWYRuQKOAlgMrWC6r+VUG0L9akaW3x0pgd0VEBX6kgqxGAQHjR9PkZ5GtqOtxwyhyxBX5/83ak3pf2Q==";

    [Fact]
    public async Task Dart_client_signature_verifies_against_the_backend()
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();

        // Register the app's REAL public key (Dart-produced SPKI blob).
        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = DartClientPublicKeySpkiBase64,
                Algorithm = "ES256",
                Label = "Flutter app (interop vector)",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        // Pin the stored challenge to the vector's fixed challenge — the server
        // would otherwise issue a random nonce the captured signature cannot
        // match. ChallengeExpiresAt uses the same fake clock the service reads.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<SimfIdentityDbContext>();
            var key = await db.DeviceKeys.SingleAsync(k => k.Id == entry.Id);
            key.CurrentChallenge = DartClientChallengeBase64;
            key.ChallengeExpiresAt = _factory.Time.SimfNow().AddMinutes(5);
            await db.SaveChangesAsync();
        }

        // Submit the Dart-produced signature through the real verify path.
        var signInResponse = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = DartClientChallengeBase64,
                Signature = DartClientSignatureBase64,
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
        // Was DEVICE_KEY_REVOKED. The endpoint is anonymous, so revoked and
        // unknown now answer identically rather than confirming the key existed.
        var body = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyNotFound, body.Error!.Code);
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

    // -- Account-lifecycle gates (S1, S2, S4) ---------------------------------
    // A device key used to be a way into the same account that honoured fewer
    // rules than the password. These four pin the gates shut.

    [Fact]
    public async Task Sign_in_is_refused_when_a_password_change_is_required()
    {
        var (visitor, userId) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entry = await EnrolDeviceKeyAsync(visitor, ecdsa);

        await UpdateUserAsync(userId, user => user.PasswordChangeRequired = true);

        var signIn = await SignInWithDeviceKeyAsync(ecdsa, entry.Id);

        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        var body = (await signIn.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthPasswordChangeRequired, body.Error!.Code);
    }

    [Fact]
    public async Task Sign_in_still_succeeds_when_no_password_change_is_pending()
    {
        // The negative of the test above: the new gate must not refuse a normal
        // account. Without this, a gate that rejected everything would pass.
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entry = await EnrolDeviceKeyAsync(visitor, ecdsa);

        var signIn = await SignInWithDeviceKeyAsync(ecdsa, entry.Id);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Sign_in_is_refused_while_the_account_is_locked_out()
    {
        var (visitor, userId) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entry = await EnrolDeviceKeyAsync(visitor, ecdsa);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(
                user, DateTimeOffset.UtcNow.AddMinutes(30));
        }

        var signIn = await SignInWithDeviceKeyAsync(ecdsa, entry.Id);

        Assert.Equal(HttpStatusCode.Locked, signIn.StatusCode);
        var body = (await signIn.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthAccountLocked, body.Error!.Code);
    }

    [Fact]
    public async Task Changing_the_password_revokes_the_device_keys()
    {
        // The remedy has to remove every credential, not just the sessions.
        // Before this, revoking the refresh tokens left the device key alive and
        // sign-in-with-device-key is anonymous, so an attacker who had enrolled
        // one simply minted a fresh session after the victim reset.
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entry = await EnrolDeviceKeyAsync(visitor, ecdsa);

        var change = await PostAuthAsync(
            "/api/v1/app/auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = AuthFlow.Password,
                NewPassword = "Ch4nged!Passw0rd#2026",
                ConfirmPassword = "Ch4nged!Passw0rd#2026",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // A revoked key cannot even take a challenge, so the ceremony is dead at
        // its first step rather than at the signature.
        var challenge = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        var body = (await challenge.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyNotFound, body.Error!.Code);
    }

    // -- Enrolment gates and bounds (S3, S5, S6, S8) ---------------------------

    [Fact]
    public async Task An_administrator_cannot_enrol_a_device_key()
    {
        // The mint issues the caller's full permission set with no second
        // factor, so an enrolled admin would hold an admin bearer obtainable
        // with one factor.
        // Promoted after sign-in rather than signed in as an admin: the register
        // endpoint reads the user fresh, so this exercises the guard without
        // dragging the Control-Panel audience and its 2FA enrolment into a
        // device-key test.
        var (token, userId) = await CreateApprovedVisitorAsync();
        await UpdateUserAsync(userId, user => user.UserType = UserType.Admin);
        var admin = token;
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
                Algorithm = "ES256",
                Label = "Admin phone",
            },
            admin);

        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.Forbidden, body.Error!.Code);
    }

    [Theory]
    [InlineData("Phone; actor=admin")]
    [InlineData("Phone=admin")]
    [InlineData("Phone\nactor=admin")]
    public async Task A_label_that_could_forge_an_audit_field_is_rejected(string label)
    {
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
                Algorithm = "ES256",
                Label = label,
            },
            visitor);

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var body = (await register.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DeviceKeyInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Enrolling_past_the_cap_revokes_the_oldest_key()
    {
        // Five is the configured cap, so the sixth enrolment must retire the
        // first rather than refuse: a user replacing a phone is never stuck.
        var (visitor, _) = await CreateApprovedVisitorAsync();
        var keys = new List<DeviceKeyEntry>();
        for (var i = 0; i < 6; i++)
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            keys.Add(await EnrolDeviceKeyAsync(visitor, ecdsa));
        }

        // Assert the contract, not the identity of the casualty. The test clock
        // can stamp several enrolments on the same instant, which leaves "oldest"
        // genuinely ambiguous, so pinning a specific key would be testing the tie
        // break rather than the cap.
        var usable = 0;
        foreach (var key in keys)
        {
            var probe = await _client.PostAsJsonAsync(
                $"/api/v1/app/auth/device-keys/{key.Id}/challenge", new { });
            if (probe.StatusCode == HttpStatusCode.OK)
            {
                usable++;
            }
        }
        Assert.Equal(5, usable);

        // Whatever was retired, the key the user is holding right now survives.
        var newest = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{keys[5].Id}/challenge", new { });
        Assert.Equal(HttpStatusCode.OK, newest.StatusCode);
    }

    [Fact]
    public async Task A_revoked_key_and_an_unknown_key_are_indistinguishable()
    {
        // The challenge endpoint is anonymous, so telling "never existed" apart
        // from "existed and was revoked" handed out free information.
        var (visitor, _) = await CreateApprovedVisitorAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var entry = await EnrolDeviceKeyAsync(visitor, ecdsa);
        await DeleteAuthAsync($"/api/v1/app/auth/device-keys/{entry.Id}", visitor);

        var revoked = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        var unknown = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{Guid.NewGuid()}/challenge", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var revokedBody = (await revoked.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        var unknownBody = (await unknown.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(unknownBody.Error!.Code, revokedBody.Error!.Code);
        Assert.Equal(unknownBody.Error!.Message, revokedBody.Error!.Message);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Registers <paramref name="ecdsa"/>'s public half for the signed-in
    /// caller and returns the created entry.</summary>
    private async Task<DeviceKeyEntry> EnrolDeviceKeyAsync(
        string accessToken, ECDsa ecdsa)
    {
        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
                Algorithm = "ES256",
                Label = "Lifecycle gate test",
            },
            accessToken);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        return (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;
    }

    /// <summary>Runs the anonymous half of the ceremony: take a challenge, sign it,
    /// submit it. Returns the raw response so the caller can assert the status.</summary>
    private async Task<HttpResponseMessage> SignInWithDeviceKeyAsync(
        ECDsa ecdsa, Guid deviceKeyId)
    {
        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{deviceKeyId}/challenge", new { });
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyChallenge>>())!.Data!;

        var signature = ecdsa.SignData(
            Convert.FromBase64String(challenge.Challenge),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = deviceKeyId,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(signature),
            });
    }

    private async Task UpdateUserAsync(Guid userId, Action<SimfUser> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = (await users.FindByIdAsync(userId.ToString()))!;
        mutate(user);
        await users.UpdateAsync(user);
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
