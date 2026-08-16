// NCA A1-19 — proves the dormant-account sweep disables Approved accounts inactive
// beyond the configured threshold, and leaves recent / non-Approved accounts alone.
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class DormantAccountTests : IClassFixture<DormantAccountApiFactory>
{
    private readonly DormantAccountApiFactory _factory;

    public DormantAccountTests(DormantAccountApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Sweep_disables_only_dormant_approved_accounts()
    {
        var t0 = _factory.Time.SimfNow();
        var stamp = Guid.NewGuid().ToString("N");

        // Created "long ago" (at t0): one Approved (should be disabled) and one
        // PendingApproval (not Approved → must be left alone).
        var dormantEmail = await CreateUserAsync($"dormant-{stamp}@simf.test", AccountState.Approved, t0);
        var pendingEmail = await CreateUserAsync($"pending-{stamp}@simf.test", AccountState.PendingApproval, t0);

        // Move 31 days forward, then create a fresh Approved account (not dormant).
        _factory.Time.Advance(TimeSpan.FromDays(31));
        var recentEmail = await CreateUserAsync(
            $"recent-{stamp}@simf.test", AccountState.Approved, _factory.Time.SimfNow());

        using (var scope = _factory.Services.CreateScope())
        {
            var sweep = scope.ServiceProvider.GetRequiredService<IDormantAccountService>();
            var disabled = await sweep.DisableDormantAccountsAsync();
            Assert.True(disabled >= 1);
        }

        Assert.Equal(AccountState.Disabled, await StateOfAsync(dormantEmail));
        Assert.Equal(AccountState.PendingApproval, await StateOfAsync(pendingEmail));
        Assert.Equal(AccountState.Approved, await StateOfAsync(recentEmail));
    }

    [Fact]
    public async Task Sweep_never_disables_administrators()
    {
        var t0 = _factory.Time.SimfNow();
        var stamp = Guid.NewGuid().ToString("N");
        // A long-dormant ADMIN (Approved + UserType.Admin) — must be left alone so
        // the sweep can never lock out the (only) admin and brick the CP.
        var adminEmail = await CreateUserAsync(
            $"dormant-admin-{stamp}@simf.test", AccountState.Approved, t0, UserType.Admin);

        _factory.Time.Advance(TimeSpan.FromDays(365));
        using (var scope = _factory.Services.CreateScope())
        {
            var sweep = scope.ServiceProvider.GetRequiredService<IDormantAccountService>();
            await sweep.DisableDormantAccountsAsync();
        }

        Assert.Equal(AccountState.Approved, await StateOfAsync(adminEmail));
    }

    private async Task<string> CreateUserAsync(
        string email, AccountState state, DateTime createdAt,
        UserType userType = UserType.Visitor)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Dormancy Test",
            AccountState = state,
            UserType = userType,
            CreatedAt = createdAt,
        };
        var result = await users.CreateAsync(user, AuthFlow.Password);
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));
        return email;
    }

    private async Task<AccountState> StateOfAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!.AccountState;
    }
}
