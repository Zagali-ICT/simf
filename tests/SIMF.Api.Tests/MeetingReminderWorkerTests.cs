// Bi-Meeting rework — tests for the 15-minute meeting-reminder scan. Exercises the
// internal MeetingReminderWorker.RunReminderScanAsync directly (InternalsVisibleTo),
// mirroring SessionReminderWorkerTests: the lead-window bound + the once-only
// ReminderSent dedup, for both the speaker and delegation meeting paths.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MeetingReminderWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public MeetingReminderWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Scan_reminds_a_confirmed_speaker_meeting_in_the_lead_window_exactly_once()
    {
        var now = DateTimeOffset.UtcNow;
        var (speakerId, userId) = await SeedSpeakerAndRequesterAsync();
        // Confirmed (Accepted) with a bound slot 10 minutes out — inside the 15-min lead.
        var requestId = await SeedConfirmedSpeakerMeetingAsync(speakerId, userId, now.AddMinutes(10));

        var firstPass = await RunScanAsync(now);
        Assert.True(firstPass >= 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

            var req = await appDb.SpeakerMeetingRequests.SingleAsync(r => r.Id == requestId);
            Assert.NotNull(req.ReminderSent);

            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.MeetingReminder
                && n.RelatedEntityId == requestId
                && n.UserId == userId);
            Assert.Equal(1, count);
        }

        // Second pass: the meeting is already stamped — no resend.
        await RunScanAsync(now);

        using (var scope = _factory.Services.CreateScope())
        {
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.MeetingReminder
                && n.RelatedEntityId == requestId
                && n.UserId == userId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Scan_ignores_a_meeting_beyond_the_lead_window()
    {
        var now = DateTimeOffset.UtcNow;
        var (speakerId, userId) = await SeedSpeakerAndRequesterAsync();
        // Starts in two hours — well beyond the 15-minute lead window.
        var requestId = await SeedConfirmedSpeakerMeetingAsync(speakerId, userId, now.AddHours(2));

        await RunScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await appDb.SpeakerMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Null(req.ReminderSent);
    }

    [Fact]
    public async Task Scan_reminds_a_confirmed_delegation_meeting_and_its_requester()
    {
        var now = DateTimeOffset.UtcNow;
        var homeId = await EnsureCountryAsync("SA", 682);
        var targetId = await EnsureCountryAsync("EG", 818);
        var userId = await SeedRequesterAsync();
        var requestId = await SeedConfirmedDelegationMeetingAsync(
            userId, homeId, targetId, now.AddMinutes(10));

        var reminded = await RunScanAsync(now);
        Assert.True(reminded >= 1);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        var req = await appDb.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.NotNull(req.ReminderSent);

        // The requester is always a recipient (plus any eligible target-delegation member).
        var count = await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.MeetingReminder
            && n.RelatedEntityId == requestId
            && n.UserId == userId);
        Assert.Equal(1, count);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<int> RunScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await MeetingReminderWorker.RunReminderScanAsync(
            db, dispatcher, now, MeetingReminderWorker.ReminderLeadTime,
            NullLogger.Instance, CancellationToken.None);
    }

    private async Task<Guid> SeedRequesterAsync()
    {
        var email = $"meeting-reminder-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Reminder Requester",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<(Guid SpeakerId, Guid UserId)> SeedSpeakerAndRequesterAsync()
    {
        var userId = await SeedRequesterAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. Reminder",
            NameArabic = "متحدث",
            Rank = "Naval Captain",
            AllowsMeetingRequests = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return (speaker.Id, userId);
    }

    private async Task<Guid> SeedConfirmedSpeakerMeetingAsync(
        Guid speakerId, Guid userId, DateTimeOffset slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = userId,
            RequesterName = "Reminder Requester",
            Subject = "Naval cooperation",
            SlotStart = slotStart,
            SlotEnd = slotStart.AddMinutes(30),
            Status = MeetingRequestStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            RespondedAt = DateTimeOffset.UtcNow,
        };
        db.SpeakerMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    private async Task<Guid> SeedConfirmedDelegationMeetingAsync(
        Guid userId, int requestingCountryId, int targetCountryId, DateTimeOffset slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = userId,
            RequestingCountryId = requestingCountryId,
            TargetCountryId = targetCountryId,
            AttendeeCount = 5,
            Subject = "Delegation cooperation",
            SlotStart = slotStart,
            SlotEnd = slotStart.AddMinutes(30),
            Status = MeetingRequestStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            RespondedAt = DateTimeOffset.UtcNow,
        };
        db.DelegationMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    private async Task<int> EnsureCountryAsync(string code, int id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id,
                Code = code,
                Name = code,
                NameArabic = code,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Countries.Add(country);
            await db.SaveChangesAsync();
        }
        return country.Id;
    }
}
