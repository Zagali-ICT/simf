// Held-item #2b / #2c — the two halves of "is the most privileged account in the
// system actually protected by more than a password".
//
// #2c: the JWT now carries an RFC 8176 `amr` claim. Before it, a token minted on a
// password alone and a token that had cleared TOTP were byte-for-byte
// indistinguishable to the authorization layer, so "this endpoint requires MFA"
// was not an expressible policy — the information simply was not in the token.
//
// #2b: Production refuses to boot without a super-admin TOTP seed, so the account
// whose permission claim is the wildcard "*" cannot be single-factor by omission.
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SecondFactorClaimTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SecondFactorClaimTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>A 2FA-off account signs in on the password alone, so its token must
    /// say so. This is the claim's whole purpose: the token is perfectly valid and
    /// perfectly usable — it is simply weaker, and now says which.</summary>
    [Fact]
    public async Task A_password_only_sign_in_is_stamped_amr_pwd()
    {
        var email = await CreateApprovedUserAsync(twoFactorEnabled: false);

        var token = await SignInAsync(email);

        Assert.Equal("pwd", AmrOf(token));
    }

    /// <summary>The negative that gives the positive its meaning: if every token
    /// were stamped "pwd", the test above would pass while proving nothing. An
    /// enrolled account's token must read "mfa".
    ///
    /// <para>Asserted through the derivation rather than by driving a live TOTP
    /// challenge, because that is exactly the path the refresh and device-key
    /// issuers take — they do not re-run the challenge, so if the derivation were
    /// wrong those two would silently downgrade a strong session to "pwd" on the
    /// next refresh.</para></summary>
    [Fact]
    public async Task An_enrolled_account_is_stamped_amr_mfa_even_when_the_issuer_did_not_run_the_challenge()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var email = $"amr-mfa-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Amr Enrolled",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
            TwoFactorEnabled = true,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var jwt = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IJwtTokenService>();
        // secondFactorCompleted omitted — the refresh / device-key case.
        var issued = jwt.CreateAccessToken(user, [], [], MobileAppRole.None);

        Assert.Equal("mfa", AmrOf(issued.Value));
    }

    /// <summary>And the explicit override still wins over the derivation, so a
    /// future issuance path can state the truth even when it contradicts the
    /// account's enrolment flag.</summary>
    [Fact]
    public async Task An_explicit_second_factor_flag_overrides_the_derivation()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var email = $"amr-override-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Amr Override",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
            TwoFactorEnabled = true,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var jwt = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.IdentityAccess.IJwtTokenService>();
        var issued = jwt.CreateAccessToken(
            user, [], [], MobileAppRole.None, secondFactorCompleted: false);

        Assert.Equal("pwd", AmrOf(issued.Value));
    }

    private static string? AmrOf(string accessToken) =>
        new JwtSecurityToken(accessToken).Claims
            .FirstOrDefault(c => c.Type == "amr")?.Value;

    private async Task<string> CreateApprovedUserAsync(bool twoFactorEnabled)
    {
        var email = $"amr-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Amr Probe",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
            TwoFactorEnabled = twoFactorEnabled,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return email;
    }

    private async Task<string> SignInAsync(string email)
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
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(
            body.Data?.Tokens?.AccessToken is not null,
            "sign-in issued no token; the fixture account may not be 2FA-off.");
        return body.Data!.Tokens!.AccessToken;
    }
}
