using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for <see cref="IdentitySeeder"/>.</summary>
public sealed class IdentitySeederTests : IClassFixture<SimfApiFactory>
{
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
            .FindByEmailAsync("superadmin@simf.test");

        Assert.NotNull(admin);
        Assert.Equal(AccountState.Approved, admin!.AccountState);
        Assert.True(admin.PasswordChangeRequired);
        Assert.True(admin.EmailConfirmed);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>()
            .FindByEmailAsync("superadmin@simf.test");
        Assert.NotNull(admin);
    }
}
