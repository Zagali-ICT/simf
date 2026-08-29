// The precedence rule for the two CP-controllable walk-in modes, pinned in both
// directions. This is the part that gets misremembered: an admin's toggle wins
// over deployment configuration, ANY non-boolean state defers to configuration,
// and neither can reach past the master switch.
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Badges)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class WalkInModeSettingsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public WalkInModeSettingsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_admin_override_wins_over_configuration_in_both_directions(
        bool overrideValue)
    {
        // Configuration says the OPPOSITE of the override in each case, so a
        // service that quietly kept reading options would fail exactly one of
        // these two rows - which is why both directions are pinned.
        using var host = ArmedWith(quickRegister: !overrideValue);
        await SetOverrideAsync(WalkInModeSettingKeys.QuickRegister, overrideValue);

        using var scope = host.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IWalkInModeSettings>();

        Assert.Equal(overrideValue, await settings.QuickRegisterActiveAsync());
    }

    [Fact]
    public async Task Clearing_an_override_hands_the_mode_back_to_configuration()
    {
        // Clearing is the only way an admin undoes their own change, so it has to
        // restore the ESTATE's posture rather than defaulting to off.
        using var host = ArmedWith(autoApprove: true);
        await SetOverrideAsync(WalkInModeSettingKeys.AutoApprove, false);

        using var scope = host.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IWalkInModeSettings>();
        Assert.False(await settings.AutoApproveActiveAsync());

        await settings.SaveAsync(
            Guid.NewGuid(), new AdminUpdateWalkInModeRequest { AutoApprove = null });

        Assert.True(await settings.AutoApproveActiveAsync());
    }

    [Fact]
    public async Task No_override_can_activate_a_mode_while_the_master_switch_is_disarmed()
    {
        // The security property the design turns on: an admin may turn a mode OFF
        // during an event, but cannot arm walk-in registration on an estate that
        // never enabled it. That still costs server access.
        using var host = _factory.WithWebHostBuilder(
            builder => builder.UseSetting("WalkInMode:Enabled", "false"));
        await SetOverrideAsync(WalkInModeSettingKeys.QuickRegister, true);
        await SetOverrideAsync(WalkInModeSettingKeys.AutoApprove, true);

        using var scope = host.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IWalkInModeSettings>();

        Assert.False(await settings.QuickRegisterActiveAsync());
        Assert.False(await settings.AutoApproveActiveAsync());

        var view = await settings.GetAsync();
        Assert.False(view.Armed);
        // The toggle still READS as on - the page shows the admin what they set,
        // and shows Armed=false beside it so an inert switch explains itself
        // instead of silently lying about its effect.
        Assert.True(view.QuickRegister);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yes")]
    [InlineData("1")]
    public async Task A_value_that_is_not_a_boolean_defers_to_configuration(string stored)
    {
        // A hand-edited row must never be able to turn a mode on by accident.
        using var host = ArmedWith(quickRegister: false);
        await SetRawAsync(WalkInModeSettingKeys.QuickRegister, stored);

        using var scope = host.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IWalkInModeSettings>();

        Assert.False(await settings.QuickRegisterActiveAsync());
        Assert.False((await settings.GetAsync()).QuickRegisterOverridden);
    }

    [Fact]
    public async Task The_view_reports_what_configuration_alone_would_give()
    {
        // So the page can say "you are overriding the deployed posture" rather
        // than showing a bare checkbox with no context.
        using var host = ArmedWith(autoApprove: false);
        await SetOverrideAsync(WalkInModeSettingKeys.AutoApprove, true);

        using var scope = host.Services.CreateScope();
        var view = await scope.ServiceProvider
            .GetRequiredService<IWalkInModeSettings>().GetAsync();

        Assert.True(view.AutoApprove);
        Assert.False(view.AutoApproveConfigured);
        Assert.True(view.AutoApproveOverridden);
    }

    private WebApplicationFactory<Program> ArmedWith(
        bool quickRegister = false, bool autoApprove = false) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WalkInMode:Enabled", "true");
            builder.UseSetting("WalkInMode:QuickRegister", quickRegister.ToString());
            builder.UseSetting("WalkInMode:AutoApprove", autoApprove.ToString());
        });

    private Task SetOverrideAsync(string key, bool value) =>
        SetRawAsync(key, value.ToString().ToLowerInvariant());

    private async Task SetRawAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.SystemSettings.SingleOrDefaultAsync(s => s.Key == key);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
        }
        else
        {
            row.Value = value;
            row.IsActive = true;
        }

        await db.SaveChangesAsync();
    }
}
