// Shared test helper for the owner-2026-07-19 rating attendance gate: a rating may
// only be submitted for something the user attended. These seed the HallAttendance
// (in-hall check-in) rows the gate reads, so a signed-in test visitor can rate.
// Session + Hall parents are created on the fly because HallAttendance needs both.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Enums;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Api.Tests;

internal static class RatingAttendance
{
    // Event-local offset (+03:00) — the codebase convention the PerDay gate uses.
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    /// <summary>The <c>SimfUser.Id</c> for a test email (Identity DB).</summary>
    internal static async Task<Guid> UserIdAsync(SimfApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email);
        return user!.Id;
    }

    /// <summary>Marks the user as having attended the event (a HallAttendance on a
    /// throwaway past session), satisfying the Global-scope rating gate.</summary>
    internal static Task SeedEventAttendanceAsync(SimfApiFactory factory, Guid userId) =>
        SeedOnNewSessionAsync(factory, userId, SimfClock.Now.AddHours(-1));

    /// <summary>Marks the user as having attended a session on the event-local day of
    /// <paramref name="dayId"/>, satisfying the PerDay gate for that programme day.</summary>
    internal static async Task SeedDayAttendanceAsync(
        SimfApiFactory factory, Guid userId, Guid dayId)
    {
        DateOnly date;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            date = await db.ProgrammeDays.Where(d => d.Id == dayId).Select(d => d.Date).SingleAsync();
        }
        // Noon on the day, in event-local time, is unambiguously inside its day window.
        var start = date.ToDateTime(new TimeOnly(12, 0));
        await SeedOnNewSessionAsync(factory, userId, start);
    }

    /// <summary>Marks the user as having attended an existing <paramref name="sessionId"/>,
    /// satisfying the PerSession gate.</summary>
    internal static async Task SeedSessionAttendanceAsync(
        SimfApiFactory factory, Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hallId = await db.Sessions.Where(s => s.Id == sessionId)
            .Select(s => s.HallId).SingleAsync();
        var profileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        db.HallAttendances.Add(NewAttendance(profileId, sessionId, hallId));
        await db.SaveChangesAsync();
    }

    /// <summary>Marks the user as having a venue-gate **Check-In** scan (Allowed) at
    /// <paramref name="scannedAt"/> with NO in-hall HallAttendance — exercises the
    /// blended gate's GateScan branch (Day / App / Event / Exhibition). Ensures a
    /// UserProfile exists (test visitors approved via SetAccountState have none) since
    /// the scan is keyed on <c>UserProfile.Id</c>, then adds a Gate + GateScan.</summary>
    internal static async Task SeedGateCheckInAsync(
        SimfApiFactory factory, Guid userId, DateTime scannedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = SimfClock.Now;

        var profileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);

        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = "G-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Main Gate", NameArabic = "البوابة الرئيسية",
            IsActive = true, CreatedAt = now,
        };
        db.Gates.Add(gate);
        db.GateScans.Add(new GateScan
        {
            GateId = gate.Id,
            UserProfileId = profileId,
            QrIdAtScan = "QR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Direction = ScanDirection.CheckIn,
            Outcome = ScanOutcome.Allowed,
            ScannedByUserId = Guid.NewGuid(),
            ScannedAt = scannedAt,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>A venue-gate Check-In scan on the event-local day of
    /// <paramref name="dayId"/> (noon, +03:00) with no in-hall attendance.</summary>
    internal static async Task SeedGateCheckInOnDayAsync(
        SimfApiFactory factory, Guid userId, Guid dayId)
    {
        DateOnly date;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            date = await db.ProgrammeDays.Where(d => d.Id == dayId).Select(d => d.Date).SingleAsync();
        }
        var scannedAt = date.ToDateTime(new TimeOnly(12, 0));
        await SeedGateCheckInAsync(factory, userId, scannedAt);
    }

    private static async Task SeedOnNewSessionAsync(
        SimfApiFactory factory, Guid userId, DateTime sessionStart)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "AH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Attendance Hall", NameArabic = "قاعة الحضور",
            Capacity = 10, IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "AS-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Attendance Session", TitleArabic = "جلسة الحضور",
            HallId = hall.Id,
            Start = sessionStart,
            End = sessionStart.AddHours(1),
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        var profileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        db.HallAttendances.Add(NewAttendance(profileId, session.Id, hall.Id));
        await db.SaveChangesAsync();
    }

    private static HallAttendance NewAttendance(
        Guid attendeeProfileId, Guid sessionId, Guid hallId) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        UserProfileId = attendeeProfileId,
        Method = AttendanceMethod.QrScan,
        Enter = SimfClock.Now,
        CreatedAt = SimfClock.Now,
    };
}
