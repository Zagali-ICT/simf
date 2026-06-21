// NCA A1-19 — proves the dormant-account sweep disables Approved accounts inactive
// beyond the configured threshold, and leaves recent / non-Approved accounts alone.
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

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
        var t0 = _factory.Time.GetUtcNow();
        var stamp = Guid.NewGuid().ToString("N");

        // Created "long ago" (at t0): one Approved (should be disabled) and one
        // PendingApproval (not Approved → must be left alone).
        var dormantEmail = await CreateUserAsync($"dormant-{stamp}@simf.test", AccountState.Approved, t0);
        var pendingEmail = await CreateUserAsync($"pending-{stamp}@simf.test", AccountState.PendingApproval, t0);

        // Move 31 days forward, then create a fresh Approved account (not dormant).
        _factory.Time.Advance(TimeSpan.FromDays(31));
        var recentEmail = await CreateUserAsync(
            $"recent-{stamp}@simf.test", AccountState.Approved, _factory.Time.GetUtcNow());

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

    private async Task<string> CreateUserAsync(string email, AccountState state, DateTimeOffset createdAt)
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
            UserType = UserType.Visitor,
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
