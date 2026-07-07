using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Domain.AccessControl;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Tests for the end-of-day + end-of-programme rating prompts (D-679). Exercises
/// the internal <see cref="ProgrammeRatingPromptWorker.RunDayPromptScanAsync"/> and
/// <see cref="ProgrammeRatingPromptWorker.RunProgramEndScanAsync"/> directly so the
/// checked-in audience join, the per-day / global dedup and the back-fill window
/// are covered without driving the BackgroundService loop.
/// </summary>
public sealed class ProgrammeRatingPromptWorkerTests : IClassFixture<SimfApiFactory>
{
    private static readonly TimeSpan Ast = TimeSpan.FromHours(3);
    private readonly SimfApiFactory _factory;

    public ProgrammeRatingPromptWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Day_scan_prompts_only_the_attendees_who_checked_in_that_day_and_stamps_once()
    {
        await DeactivateExistingDaysAsync(); // isolate from sibling tests (one active day per date)
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-3);
        var end = now.AddHours(-2);
        var dayDate = DateOnly.FromDateTime(start.ToOffset(Ast).DateTime);

        var dayId = await SeedDayWithSessionAsync(dayDate, start, end);
        var checkedIn = await SeedApprovedVisitorAsync();
        await SeedProfileWithScanAsync(checkedIn, scanUtc: start);         // in the day window
        var absent = await SeedApprovedVisitorAsync();
        await SeedProfileWithScanAsync(absent, scanUtc: now.AddDays(-3));  // a different day

        var prompted = await RunDayScanAsync(now);
        Assert.True(prompted >= 1);

        await AssertDayStampedAsync(dayId);
        Assert.Equal(1, await CountAsync(NotificationKind.DayRatingRequest, checkedIn, dayId));
        Assert.Equal(0, await CountAsync(NotificationKind.DayRatingRequest, absent, dayId));

        // Second pass: the day is already stamped — no resend.
        await RunDayScanAsync(now);
        Assert.Equal(1, await CountAsync(NotificationKind.DayRatingRequest, checkedIn, dayId));
    }

    [Fact]
    public async Task Day_scan_ignores_a_day_that_ended_before_the_backfill_window()
    {
        await DeactivateExistingDaysAsync(); // isolate from sibling tests (one active day per date)
        var now = DateTimeOffset.UtcNow;
        // A session-less day two days ago → its end (next local midnight) is well
        // beyond the 24h back-fill window.
        var dayDate = DateOnly.FromDateTime(now.ToOffset(Ast).DateTime).AddDays(-2);
        var dayId = await SeedDayAsync(dayDate);

        await RunDayScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var day = await db.ProgrammeDays.SingleAsync(d => d.Id == dayId);
        Assert.Null(day.RatingPromptSentUtc);
    }

    [Fact]
    public async Task Program_end_scan_dispatches_the_trio_once_to_checked_in_attendees()
    {
        await DeactivateExistingDaysAsync();
        await ClearProgramEndMarkerAsync();

        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(-3);
        var end = now.AddHours(-2);
        var lastDate = DateOnly.FromDateTime(start.ToOffset(Ast).DateTime);
        await SeedDayAsync(lastDate.AddDays(-2));
        await SeedDayAsync(lastDate.AddDays(-1));
        await SeedDayWithSessionAsync(lastDate, start, end); // the max day, just ended

        var checkedIn = await SeedApprovedVisitorAsync();
        await SeedProfileWithScanAsync(checkedIn, scanUtc: start);

        var fired = await RunProgramEndScanAsync(now);
        Assert.True(fired);

        Assert.Equal(1, await CountAsync(NotificationKind.EventRatingRequest, checkedIn, null));
        Assert.Equal(1, await CountAsync(NotificationKind.ExhibitionRatingRequest, checkedIn, null));
        Assert.Equal(1, await CountAsync(NotificationKind.AppRatingRequest, checkedIn, null));
        Assert.True(await ProgramEndMarkerExistsAsync());

        // Second pass: the global marker is set — no resend.
        var again = await RunProgramEndScanAsync(now);
        Assert.False(again);
        Assert.Equal(1, await CountAsync(NotificationKind.EventRatingRequest, checkedIn, null));
    }

    // -- Runners ---------------------------------------------------------------

    private async Task<int> RunDayScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await ProgrammeRatingPromptWorker.RunDayPromptScanAsync(
            db, dispatcher, now, ProgrammeRatingPromptWorker.BackfillWindow,
            NullLogger.Instance, CancellationToken.None);
    }

    private async Task<bool> RunProgramEndScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await ProgrammeRatingPromptWorker.RunProgramEndScanAsync(
            db, dispatcher, now, ProgrammeRatingPromptWorker.BackfillWindow,
            NullLogger.Instance, CancellationToken.None);
    }

    // -- Assertions ------------------------------------------------------------

    private async Task<int> CountAsync(NotificationKind kind, Guid userId, Guid? relatedId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await idDb.Notifications.CountAsync(n =>
            n.Kind == kind && n.UserId == userId
            && (relatedId == null || n.RelatedEntityId == relatedId));
    }

    private async Task AssertDayStampedAsync(Guid dayId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var day = await db.ProgrammeDays.SingleAsync(d => d.Id == dayId);
        Assert.NotNull(day.RatingPromptSentUtc);
    }

    private async Task<bool> ProgramEndMarkerExistsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.SystemSettings.AnyAsync(
            s => s.Key == ProgrammeRatingPromptWorker.ProgramEndSettingKey);
    }

    // -- Seeding ---------------------------------------------------------------

    private async Task<Guid> SeedApprovedVisitorAsync()
    {
        var email = $"day-rate-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Day Rate Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task SeedProfileWithScanAsync(Guid userId, DateTimeOffset scanUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor " + Guid.NewGuid().ToString("N")[..4],
            NameArabic = "زائر",
            PageColor = "#0EA5E9",
            IsForVisitor = true,
            MobileAppRole = MobileAppRole.None,
            IsActive = true,
            CreatedAt = now,
        };
        db.ProfileTypes.Add(profileType);

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileTypeId = profileType.Id,
            Name = "Attendee",
            NameArabic = "حاضر",
            NationalityId = 682,
            CreatedAt = now,
        };
        db.UserProfiles.Add(profile);

        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = "G-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Main Gate",
            NameArabic = "البوابة الرئيسية",
            IsActive = true,
            CreatedAt = now,
        };
        db.Gates.Add(gate);

        db.GateScans.Add(new GateScan
        {
            GateId = gate.Id,
            UserProfileId = profile.Id,
            QrIdAtScan = "QR" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Direction = ScanDirection.CheckIn,
            Outcome = ScanOutcome.Allowed,
            ScannedByUserId = Guid.NewGuid(),
            ScannedAtUtc = scanUtc,
        });

        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedDayAsync(DateOnly date)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var day = new ProgrammeDay
        {
            Id = Guid.NewGuid(),
            Date = date,
            Title = "Day " + date,
            TitleArabic = "اليوم",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ProgrammeDays.Add(day);
        await db.SaveChangesAsync();
        return day.Id;
    }

    private async Task<Guid> SeedDayWithSessionAsync(DateOnly date, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var dayId = await SeedDayAsync(date);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall",
            NameArabic = "قاعة",
            Capacity = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Talk",
            TitleArabic = "جلسة",
            HallId = hall.Id,
            StartUtc = startUtc,
            EndUtc = endUtc,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return dayId;
    }

    private async Task DeactivateExistingDaysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var days = await db.ProgrammeDays.Where(d => d.IsActive).ToListAsync();
        foreach (var day in days)
        {
            day.IsActive = false;
        }
        await db.SaveChangesAsync();
    }

    private async Task ClearProgramEndMarkerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.SystemSettings
            .Where(s => s.Key == ProgrammeRatingPromptWorker.ProgramEndSettingKey)
            .ToListAsync();
        if (rows.Count > 0)
        {
            db.SystemSettings.RemoveRange(rows);
            await db.SaveChangesAsync();
        }
    }
}
