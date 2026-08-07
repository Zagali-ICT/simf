// Pins the Control Panel 2FA enrolment gate ON for the GENERAL integration suite.
//
// Why this file exists, and why a comment would not have done instead:
//
// Until 2026-07-31 SimfApiFactory pinned
// IdentityLifecycle__RequireControlPanelTwoFactorEnrolment to "false", so the ~150
// admin fixtures in this assembly signed in with a password alone and exercised the
// PRE-FIX path. That pin is gone and AuthFlow.SignInControlPanelAsync now enrols the
// account and completes a real TOTP step.
//
// The hole that left: nothing ASSERTED the gate was on. Flip the value in
// SimfApiFactory back to "false" and every admin test still passed, because AuthFlow
// accepted password-only tokens as a valid outcome. The security posture would have
// silently reverted with a green suite. AuthFlow now throws on that path, and this
// class pins the gate from the outside — per the global rule "pin the surface with a
// test, not a comment" (CLAUDE.md §4).
//
// The enrolment CONTRACT (challenge shape, ticket single-use, wrong-code refusal) is
// proved in ControlPanelTwoFactorEnrolmentTests against its own factory. This is only
// the pin, and it deliberately uses the SAME SimfApiFactory the ~150 fixtures use.
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ControlPanelTwoFactorGatePinTests : IClassFixture<SimfApiFactory>
{
    // Tests: this file IS the test.

    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory factory;

    public ControlPanelTwoFactorGatePinTests(SimfApiFactory factory)
    {
        this.factory = factory;
        // Once per class, as every other fixture in this assembly does — the
        // factory names a fresh database per instance but does not migrate it
        // until asked, so a class that skips this reads "Cannot open database".
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public void The_general_test_factory_runs_with_the_enrolment_gate_ON()
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityLifecycleOptions>>().Value;

        Assert.True(
            options.RequireControlPanelTwoFactorEnrolment,
            "The general integration suite must run with the Control Panel 2FA "
            + "enrolment gate ON — the production default. It was pinned off until "
            + "2026-07-31, which meant ~150 admin fixtures exercised the pre-fix "
            + "single-factor path. Do not pin it off again to make a fixture pass: "
            + "enrol the account through AuthFlow.SignInControlPanelAsync instead.");
    }

    [Fact]
    public async Task A_Cp_password_step_alone_never_returns_a_session()
    {
        // The behaviour the gate exists for, proved through the wire rather than by
        // reading a configuration value: a Control Panel admin who has NOT paired an
        // authenticator gets a challenge and NO tokens from the password step.
        var email = await CreateUnenrolledAdminAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });

        var body = await response.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>();
        Assert.NotNull(body);
        Assert.Null(body!.Data?.Tokens);
    }

    private async Task<string> CreateUnenrolledAdminAsync()
    {
        var email = $"gate-pin-{Guid.NewGuid():N}@simf.test";
        using var scope = factory.Services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            await roleManager.CreateAsync(
                new SimfRole { Name = AdministratorRole, IsBaseline = true });
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Gate Pin Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        await users.AddToRoleAsync(user, AdministratorRole);
        return email;
    }
}
