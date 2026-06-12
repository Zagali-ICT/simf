using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for <see cref="IdentitySeeder"/>.</summary>
public sealed class IdentitySeederTests : IClassFixture<SimfApiFactory>
{
    private const string SuperAdminEmail = "superadmin@simf.test";

    private readonly SimfApiFactory _factory;

    public IdentitySeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task SeedAsync_creates_the_super_admin()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        await services.GetRequiredService<IdentitySeeder>().SeedAsync();

        var admin = await services.GetRequiredService<UserManager<SimfUser>>()
            .FindByEmailAsync(SuperAdminEmail);

        Assert.NotNull(admin);
        Assert.Equal(AccountState.Approved, admin!.AccountState);
        Assert.True(admin.PasswordChangeRequired);
        Assert.True(admin.EmailConfirmed);
    }

    [Fact]
    public async Task SeedAsync_provisions_the_super_admin_TOTP_secret()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var admin = await userManager.FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
        Assert.True(await userManager.GetTwoFactorEnabledAsync(admin!));
        Assert.Equal("JBSWY3DPEHPK3PXP", await userManager.GetAuthenticatorKeyAsync(admin!));
    }

    [Fact]
    public async Task SeedAsync_writes_a_SuperAdminSeeded_audit_entry()
    {
        using var scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Contains(
            database.OperationLog,
            entry => entry.EventType == AuditEvents.SuperAdminSeeded);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>()
            .FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task SeedAsync_seeds_the_registration_baseline_lookups_and_core_content()
    {
        // D-377 — the profile save REQUIRES interests + an organisation, so a
        // fresh environment must boot with both populated, plus the app's
        // terms/about content blocks. Double-run also proves idempotency
        // (the counts must not grow on the second pass).
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var interestCount = database.Interests.Count();
        var organisationCount = database.Organisations.Count();

        Assert.True(interestCount > 0, "baseline interests must be seeded");
        Assert.True(organisationCount > 0, "baseline organisations must be seeded");
        Assert.Contains(database.ContentBlocks, b => b.Key == "terms" && b.IsActive);
        Assert.Contains(database.ContentBlocks, b => b.Key == "about" && b.IsActive);
        // The "Other — not listed" catch-all keeps a visitor whose organisation
        // is missing from being blocked.
        Assert.Contains(database.Organisations, o => o.Name == "Other — not listed");

        await seeder.SeedAsync();
        Assert.Equal(interestCount, database.Interests.Count());
        Assert.Equal(organisationCount, database.Organisations.Count());
    }
}
