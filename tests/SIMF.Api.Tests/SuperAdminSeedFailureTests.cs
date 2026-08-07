// OP-SUPERADMIN-SEED (2026-07-30) — the super-admin seed used to fail SILENTLY.
//
// IdentitySeeder.CreateSuperAdminAsync logged the Identity errors and returned
// null; SeedAsync's caller saw the null and returned. The API then finished
// booting and reported healthy with NO super-administrator row, so the Control
// Panel had no way in and nobody found out until someone tried to sign in.
//
// Program.cs's production guard does not cover this: it only rejects the exact
// committed DEFAULT temp password, so any CUSTOM value that merely violates the
// password policy (too short, no digit, no symbol) walks straight past it.
//
// These tests drive the real seeder with a deliberately policy-violating
// password. Rather than boot a second host under the Production environment —
// which would trip five unrelated production guards and prove nothing about this
// one — they construct IdentitySeeder from the live scope with only the
// IHostEnvironment and the SuperAdmin options substituted.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SIMF.Common.Options;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SuperAdminSeedFailureTests : IClassFixture<SimfApiFactory>
{
    // Fails the Identity policy on every count that matters: too short, no
    // digit, no upper case, no non-alphanumeric. Not a credential — it can
    // never create an account, which is the whole point of the fixture.
    private const string PolicyViolatingPassword = "abc";

    private readonly SimfApiFactory _factory;

    public SuperAdminSeedFailureTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>In Production a bootstrap account that cannot be created is a
    /// failed deployment: the boot must stop rather than come up healthy with a
    /// Control Panel nobody can enter.</summary>
    [Fact]
    public async Task The_seed_throws_in_Production_when_the_temp_password_violates_the_policy()
    {
        var email = $"seedfail-prod-{Guid.NewGuid():N}@simf.test";

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunSeedAsync(Environments.Production, email, PolicyViolatingPassword));

        // The operator has to be able to act on it, so the message must name the
        // setting and carry the Identity reasons rather than say "seed failed".
        Assert.Contains("SuperAdmin:TempPassword", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Control Panel", thrown.Message, StringComparison.Ordinal);
        Assert.True(
            thrown.Message.Length > "SuperAdmin:TempPassword".Length + 40,
            "The failure message carries no policy reason, so the operator still "
            + "has to guess which rule the configured password broke: "
            + thrown.Message);

        Assert.False(await UserExistsAsync(email));
    }

    /// <summary>Outside Production the log-and-skip behaviour stands — a
    /// developer on a half-configured box must still be able to start the app.
    /// This is the half that stops the fix becoming a new outage.</summary>
    [Fact]
    public async Task The_seed_logs_and_skips_in_Development_when_the_temp_password_violates_the_policy()
    {
        var email = $"seedfail-dev-{Guid.NewGuid():N}@simf.test";

        await RunSeedAsync(Environments.Development, email, PolicyViolatingPassword);

        Assert.False(await UserExistsAsync(email));
    }

    /// <summary>The guard must fire only on failure: a compliant password still
    /// seeds the account in Production. Without this the two tests above would
    /// pass against a seeder that always threw.</summary>
    [Fact]
    public async Task The_seed_creates_the_super_admin_in_Production_when_the_password_is_compliant()
    {
        var email = $"seedok-prod-{Guid.NewGuid():N}@simf.test";

        await RunSeedAsync(Environments.Production, email, "Zx9#mKp2!Seed");

        Assert.True(await UserExistsAsync(email));
    }

    // ---------------------------------------------------------------------

    /// <summary>Runs the real seeder with only the environment and the
    /// SuperAdmin options replaced; every other dependency is the live
    /// registration.</summary>
    private async Task RunSeedAsync(
        string environmentName, string email, string tempPassword)
    {
        using var scope = _factory.Services.CreateScope();
        var options = Options.Create(new SuperAdminOptions
        {
            Email = email,
            TempPassword = tempPassword,
            // No TOTP secret: the seeder only pairs one when it is configured,
            // and this fixture is about the create step.
            TotpSecret = string.Empty,
            PasswordChangeRequired = true,
        });

        var seeder = ActivatorUtilities.CreateInstance<IdentitySeeder>(
            scope.ServiceProvider,
            options,
            new StubHostEnvironment(environmentName));
        await seeder.SeedAsync();
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await db.Users.AnyAsync(user => user.Email == email);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "SIMF.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
