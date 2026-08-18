// G-2 — the close-out worker stamps Leave = Session.End on open
// HallAttendance rows whose session has ended (In-only hall-door gates never emit
// a departure), and leaves rows of still-live sessions untouched.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Enums;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests.Operations;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class HallAttendanceCloseoutWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public HallAttendanceCloseoutWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Closes_open_rows_of_ended_sessions_stamping_leave_utc_to_end()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var (hall, session) = SeedSession(db, start: now.AddHours(-3), end: now.AddHours(-1));
        var row = await SeedOpenAttendanceAsync(db, session, hall);
        await db.SaveChangesAsync();

        var closed = await HallAttendanceCloseoutWorker.CloseEndedSessionsAsync(db, now, default);

        Assert.Equal(1, closed);
        var reloaded = await db.HallAttendances.SingleAsync(a => a.Id == row.Id);
        Assert.Equal(session.End, reloaded.Leave);
        Assert.NotNull(reloaded.UpdatedAt);
    }

    [Fact]
    public async Task Leaves_open_rows_of_live_sessions_untouched()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var (hall, session) = SeedSession(db, start: now.AddMinutes(-15), end: now.AddMinutes(45));
        var row = await SeedOpenAttendanceAsync(db, session, hall);
        await db.SaveChangesAsync();

        var closed = await HallAttendanceCloseoutWorker.CloseEndedSessionsAsync(db, now, default);

        Assert.Equal(0, closed);
        var reloaded = await db.HallAttendances.SingleAsync(a => a.Id == row.Id);
        Assert.Null(reloaded.Leave);
    }

    [Fact]
    public async Task Already_closed_rows_are_ignored()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var (hall, session) = SeedSession(db, start: now.AddHours(-3), end: now.AddHours(-1));
        var row = await SeedOpenAttendanceAsync(db, session, hall);
        row.Leave = now.AddHours(-1); // already departed
        await db.SaveChangesAsync();

        var closed = await HallAttendanceCloseoutWorker.CloseEndedSessionsAsync(db, now, default);
        Assert.Equal(0, closed);
    }

    private static (Hall Hall, Session Session) SeedSession(
        SimfAppDbContext db, DateTime start, DateTime end)
    {
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Closeout Hall", NameArabic = "قاعة الإغلاق",
            Capacity = 100, IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Closeout Session", TitleArabic = "جلسة الإغلاق",
            HallId = hall.Id,
            Start = start, End = end,
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        return (hall, session);
    }

    /// <summary>The attendee is a real profile with NO account — the walk-in this
    /// worker most often closes out. An invented Guid no longer works: the row now
    /// carries a real foreign key to the attendee record.</summary>
    private static async Task<HallAttendance> SeedOpenAttendanceAsync(
        SimfAppDbContext db, Session session, Hall hall)
    {
        var row = new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserProfileId = await TestAttendeeProfiles.CreateAccountlessAsync(db),
            Method = AttendanceMethod.QrScan,
            Enter = session.Start,
            CreatedAt = SimfClock.Now,
        };
        db.HallAttendances.Add(row);
        return row;
    }
}
