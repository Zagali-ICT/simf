// itokenissuer-extraction (2026-07-30) — token minting was duplicated across the
// auth paths, so the claim set and the D-443 absolute session cap could drift
// between the ways into the system without anything failing.
//
// It already had: DeviceKeyService.MintTokensAsync never recorded
// LastSuccessfulSignInAt, so an attendee who only ever signed in with Face ID
// looked inactive to the A1-19 dormant sweep and could be auto-disabled while
// using the app daily; it also returned no PreviousSignInAt, so the "last
// signed in ..." notice was blank on that path only.
//
// These tests pin the three entry points together. They compare CLAIM TYPES and
// the persisted refresh-token lifetime, not claim values: `sub`, `jti` and the
// `amr` strength marker legitimately differ per sign-in. A drift that these
// would miss is one that adds no claim and changes no lifetime.
using System.IdentityModel.Tokens.Jwt;
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
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class TokenIssuerParityTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public TokenIssuerParityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>Password sign-in and badge-QR sign-in mint the same claim set —
    /// badge sign-in delegates to the password pipeline, and this stops that
    /// delegation being replaced by a second copy.</summary>
    [Fact]
    public async Task Password_and_badge_sign_in_mint_the_same_claim_set()
    {
        var (email, qrId) = await CreateApprovedVisitorAsync();

        var byPassword = await SignInWithPasswordAsync(email);
        var byBadge = await SignInWithBadgeAsync(qrId);

        Assert.Equal(ClaimTypesOf(byPassword.AccessToken), ClaimTypesOf(byBadge.AccessToken));
    }

    /// <summary>The device-key ceremony mints the same claim set as the password
    /// path. This is the pair that had actually diverged.</summary>
    [Fact]
    public async Task Password_and_device_key_sign_in_mint_the_same_claim_set()
    {
        var (email, _) = await CreateApprovedVisitorAsync();

        var byPassword = await SignInWithPasswordAsync(email);
        var byDeviceKey = await SignInWithDeviceKeyAsync(byPassword.AccessToken);

        Assert.Equal(ClaimTypesOf(byPassword.AccessToken), ClaimTypesOf(byDeviceKey.AccessToken));
    }

    /// <summary>D-443 — the absolute session cap is stamped on the refresh token
    /// at mint time. All three entry points must stamp the SAME window, or one of
    /// them silently grants a longer session than the NCA finding allows.</summary>
    [Fact]
    public async Task Every_entry_point_stamps_the_same_D443_session_lifetime()
    {
        var (email, qrId) = await CreateApprovedVisitorAsync();

        var byPassword = await SignInWithPasswordAsync(email);
        var byBadge = await SignInWithBadgeAsync(qrId);
        var byDeviceKey = await SignInWithDeviceKeyAsync(byPassword.AccessToken);

        var lifetimes = new[]
        {
            await RefreshTokenLifetimeAsync(byPassword.RefreshToken),
            await RefreshTokenLifetimeAsync(byBadge.RefreshToken),
            await RefreshTokenLifetimeAsync(byDeviceKey.RefreshToken),
        };

        // The clock is a FakeTimeProvider held still for the whole class, so the
        // three windows are identical to the tick rather than merely close.
        Assert.Equal(lifetimes[0], lifetimes[1]);
        Assert.Equal(lifetimes[0], lifetimes[2]);
        Assert.True(
            lifetimes[0] > TimeSpan.Zero,
            "The refresh token carries no absolute deadline at all, so the D-443 "
            + "24h session cap is not being applied.");
    }

    /// <summary>The regression the extraction actually fixed: a device-key
    /// sign-in now counts as activity, so a biometric-only user is no longer a
    /// candidate for the A1-19 dormant-account sweep.</summary>
    [Fact]
    public async Task A_device_key_sign_in_records_the_account_as_active()
    {
        var (email, _) = await CreateApprovedVisitorAsync();
        var byPassword = await SignInWithPasswordAsync(email);

        await ClearLastSignInAsync(email);
        await SignInWithDeviceKeyAsync(byPassword.AccessToken);

        Assert.NotNull(await LastSignInAsync(email));
    }

    // ---------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------

    private const string QrAlphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    private async Task<(string Email, string QrId)> CreateApprovedVisitorAsync()
    {
        var email = $"issuer-{Guid.NewGuid():N}@simf.test";

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Issuer Parity Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
            // 2FA off so the password step issues tokens directly — the App
            // audience is not affected by the #2 Control-Panel enrolment gate.
            TwoFactorEnabled = false,
        };
        var created = await users.CreateAsync(user, AuthFlow.Password);
        Assert.True(
            created.Succeeded,
            string.Join("; ", created.Errors.Select(error => error.Description)));

        // D-157 — the badge QR lives on the App-side UserProfile, resolved by a
        // second query against a bare Guid; there is no cross-database FK.
        var qrId = NewQrId();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Issuer Parity Visitor",
            NameArabic = "زائر",
            QrId = qrId,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return (email, qrId);
    }

    /// <summary>A 12-char Crockford-base32 id matching the QR minter's alphabet
    /// and length, unique per call.</summary>
    private static string NewQrId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var chars = new char[12];
        for (var i = 0; i < 12; i++)
        {
            chars[i] = QrAlphabet[bytes[i] % QrAlphabet.Length];
        }
        return new string(chars);
    }

    private async Task<AuthTokens> SignInWithPasswordAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.App,
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.NotNull(body.Data!.Tokens);
        return body.Data.Tokens!;
    }

    private async Task<AuthTokens> SignInWithBadgeAsync(string qrId)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/badge-sign-in",
            new BadgeSignInRequest { QrId = qrId, Password = AuthFlow.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.NotNull(body.Data!.Tokens);
        return body.Data.Tokens!;
    }

    /// <summary>Runs the full ECDSA P-256 ceremony: register the public key with
    /// an existing session, take a challenge, sign it, exchange it for tokens.</summary>
    private async Task<AuthTokens> SignInWithDeviceKeyAsync(string accessToken)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var register = await PostAuthAsync(
            "/api/v1/app/auth/device-keys",
            new RegisterDeviceKeyRequest
            {
                PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
                Algorithm = "ES256",
                Label = "Issuer parity probe",
            },
            accessToken);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var entry = (await register.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyEntry>>())!.Data!;

        var challengeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/app/auth/device-keys/{entry.Id}/challenge", new { });
        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = (await challengeResponse.Content
            .ReadFromJsonAsync<ApiResult<DeviceKeyChallenge>>())!.Data!;

        var signature = ecdsa.SignData(
            Convert.FromBase64String(challenge.Challenge),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var signIn = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in-with-device-key",
            new SignInWithDeviceKeyRequest
            {
                DeviceKeyId = entry.Id,
                Challenge = challenge.Challenge,
                Signature = Convert.ToBase64String(signature),
            });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return (await signIn.Content.ReadFromJsonAsync<ApiResult<AuthTokens>>())!.Data!;
    }

    private async Task<TimeSpan> RefreshTokenLifetimeAsync(string refreshToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var hash = SIMF.Application.IdentityAccess.OpaqueToken.Hash(refreshToken);
        var row = await db.RefreshTokens.SingleAsync(token => token.TokenHash == hash);
        return row.ExpiresAt - row.CreatedAt;
    }

    private async Task ClearLastSignInAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        user.LastSuccessfulSignInAt = null;
        await db.SaveChangesAsync();
    }

    private async Task<DateTime?> LastSignInAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Email == email);
        return user.LastSuccessfulSignInAt;
    }

    private static IReadOnlyList<string> ClaimTypesOf(string accessToken) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken)
            .Claims
            .Select(claim => claim.Type)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToList();

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
