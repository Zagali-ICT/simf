using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Seeding;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// BUG-023 regression — a seeded QA database carried NO operational configuration
/// (Gates=0, GateAssignments=0, GateProfileTypeAllow=0, SessionModerators=0,
/// HallSeatLayouts=0), so the gate scanner, the moderation desk and the seat picker
/// were all untestable: the guards were correct, the data was missing.
/// <see cref="DemoOperationalConfigSeeder"/> now seeds it alongside the demo
/// accounts. The fixture already runs it once in
/// <see cref="SimfApiFactory.EnsureDatabaseCreated"/>; these tests re-run it to
/// assert idempotency and inspect the result.
/// </summary>
public sealed class DemoOperationalConfigSeederTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public DemoOperationalConfigSeederTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Seeds_a_gate_assigned_to_the_demo_staff_operator()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        var staff = await users.FindByEmailAsync("staff@simf.local");
        Assert.NotNull(staff);

        // The general venue gate — no allow rows, so every badge passes.
        var main = await db.Gates
            .Include(gate => gate.Assignments)
            .Include(gate => gate.AllowedProfileTypes)
            .SingleAsync(gate => gate.Code == "MAIN-GATE");
        Assert.True(main.IsActive);
        Assert.Empty(main.AllowedProfileTypes);
        Assert.Contains(main.Assignments, a => a.UserId == staff!.Id && a.IsActive);

        // The restricted gate — allow rows for the two VIP tiers, so the
        // constraint engine's allow-list branch has data too.
        var vip = await db.Gates
            .Include(gate => gate.Assignments)
            .Include(gate => gate.AllowedProfileTypes)
            .SingleAsync(gate => gate.Code == "VIP-GATE");
        Assert.Equal(2, vip.AllowedProfileTypes.Count);
        Assert.Contains(vip.Assignments, a => a.UserId == staff!.Id && a.IsActive);

        var vipTierIds = await db.ProfileTypes
            .Where(t => t.Name == "VVIP" || t.Name == "VIP")
            .Select(t => t.Id)
            .ToListAsync();
        Assert.All(
            vip.AllowedProfileTypes,
            allow => Assert.Contains(allow.ProfileTypeId, vipTierIds));
    }

    [Fact]
    public async Task Seeds_the_demo_moderator_onto_the_programme_QA_sessions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        var moderator = await users.FindByEmailAsync("moderator@simf.local");
        Assert.NotNull(moderator);

        var grantedSessionCodes = await db.SessionModerators
            .Where(grant => grant.UserId == moderator!.Id)
            .Join(db.Sessions, grant => grant.SessionId, session => session.Id,
                (grant, session) => session.Code)
            .ToListAsync();

        Assert.NotEmpty(grantedSessionCodes);
        Assert.Contains("D1-17", grantedSessionCodes); // Panel 1
        Assert.Contains("D3-06", grantedSessionCodes); // Axis 5 - Q&A Session
    }

    [Fact]
    public async Task Seeds_a_seat_layout_for_the_main_hall()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var hall = await db.Halls.SingleAsync(h => h.Code == "MAIN");
        var layout = await db.HallSeatLayouts.SingleAsync(l => l.HallId == hall.Id);

        var rows = layout.RowLabels.Split(',');
        Assert.Equal(10, rows.Length);
        Assert.Equal(20, layout.SeatsPerRow);
        // The same rule SeatReservationService.SetLayoutAsync enforces.
        Assert.True(rows.Length * layout.SeatsPerRow <= hall.Capacity);
    }

    [Fact]
    public async Task Re_running_the_seed_adds_nothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var gatesBefore = await db.Gates.CountAsync();
        var assignmentsBefore = await db.GateAssignments.CountAsync();
        var allowsBefore = await db.GateProfileTypeAllows.CountAsync();
        var moderatorsBefore = await db.SessionModerators.CountAsync();
        var layoutsBefore = await db.HallSeatLayouts.CountAsync();

        await scope.ServiceProvider.GetRequiredService<DemoOperationalConfigSeeder>()
            .SeedAsync(CancellationToken.None);

        Assert.Equal(gatesBefore, await db.Gates.CountAsync());
        Assert.Equal(assignmentsBefore, await db.GateAssignments.CountAsync());
        Assert.Equal(allowsBefore, await db.GateProfileTypeAllows.CountAsync());
        Assert.Equal(moderatorsBefore, await db.SessionModerators.CountAsync());
        Assert.Equal(layoutsBefore, await db.HallSeatLayouts.CountAsync());
    }
}
