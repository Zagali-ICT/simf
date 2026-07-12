// Tests: R-1a — MeetingAwaitingSpeakerExpiryWorker.RunExpiryScanAsync. A stuck
// AwaitingSpeaker request (its 72h double-opt-in tokens all expired/consumed) is
// reverted to a clean Pending and its hall binding + slot + window are cleared; a
// request that still has a live token, and any decided request, are left untouched.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MeetingAwaitingSpeakerExpiryWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public MeetingAwaitingSpeakerExpiryWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Reverts_an_AwaitingSpeaker_request_whose_tokens_all_expired_back_to_Pending_and_clears_the_hall_binding()
    {
        var now = DateTimeOffset.UtcNow;
        // A hall-bound AwaitingSpeaker request whose only token pair has expired.
        var requestId = await SeedBoundAwaitingRequestAsync(
            slotStart: now.AddDays(3), tokenExpiresUtc: now.AddHours(-1), tokenUsed: false);

        var reverted = await RunScanAsync(now);
        Assert.Equal(1, reverted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
        Assert.Null(req.HallId);
        Assert.Null(req.MeetingTableId);
        Assert.Null(req.SlotStartUtc);
        Assert.Null(req.SlotEndUtc);
        Assert.Null(req.AvailabilityWindowId);
        Assert.Null(req.RespondedAt);
        Assert.Null(req.RespondedByUserId);
        Assert.Null(req.ResponseNote);

        // The revert is audited.
        var audited = await db.OperationLog.AnyAsync(e =>
            e.EventType == AuditEvents.SpeakerMeetingRequestReverted
            && e.Detail!.Contains(requestId.ToString()));
        Assert.True(audited);
    }

    [Fact]
    public async Task Does_not_revert_an_AwaitingSpeaker_request_that_still_has_an_unused_unexpired_token()
    {
        var now = DateTimeOffset.UtcNow;
        // The token pair is still live (unused + 48h left).
        var requestId = await SeedBoundAwaitingRequestAsync(
            slotStart: now.AddDays(3), tokenExpiresUtc: now.AddHours(48), tokenUsed: false);

        var reverted = await RunScanAsync(now);
        Assert.Equal(0, reverted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, req.Status);
        Assert.NotNull(req.SlotStartUtc);
    }

    [Fact]
    public async Task Does_not_touch_Accepted_or_Rejected_requests()
    {
        var now = DateTimeOffset.UtcNow;
        var acceptedId = await SeedRequestAsync(MeetingRequestStatus.Accepted, now.AddDays(3));
        var rejectedId = await SeedRequestAsync(MeetingRequestStatus.Rejected, null);

        var reverted = await RunScanAsync(now);
        Assert.Equal(0, reverted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(MeetingRequestStatus.Accepted,
            (await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == acceptedId)).Status);
        Assert.Equal(MeetingRequestStatus.Rejected,
            (await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == rejectedId)).Status);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<int> RunScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        return await MeetingAwaitingSpeakerExpiryWorker.RunExpiryScanAsync(
            db, auditLog, now, CancellationToken.None);
    }

    // Seeds a fully hall-bound AwaitingSpeaker request (hall + table + slot + window)
    // plus one Approve token with the given expiry/used state.
    private async Task<Guid> SeedBoundAwaitingRequestAsync(
        DateTimeOffset slotStart, DateTimeOffset tokenExpiresUtc, bool tokenUsed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK" + suffix,
            Name = "Speaker", NameArabic = "متحدّث",
            IsActive = true, AllowsMeetingRequests = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H" + suffix,
            Name = "Meeting Hall", NameArabic = "قاعة",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);

        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Code = "T" + suffix,
            Capacity = 4, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MeetingTables.Add(table);

        var window = new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            StartUtc = slotStart,
            EndUtc = slotStart.AddHours(2),
            SlotMinutes = 30, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SpeakerAvailabilityWindows.Add(window);

        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Requester", Subject = "Topic",
            Status = MeetingRequestStatus.AwaitingSpeaker,
            HallId = hall.Id,
            MeetingTableId = table.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddMinutes(30),
            AvailabilityWindowId = window.Id,
            RespondedAt = DateTimeOffset.UtcNow,
            RespondedByUserId = Guid.NewGuid(),
            ResponseNote = "bound",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SpeakerMeetingRequests.Add(req);

        db.MeetingActionTokens.Add(new MeetingActionToken
        {
            Id = Guid.NewGuid(),
            SpeakerMeetingRequestId = req.Id,
            Action = MeetingActionType.Approve,
            TokenHash = "hash-" + Guid.NewGuid().ToString("N"),
            ExpiresUtc = tokenExpiresUtc,
            UsedAt = tokenUsed ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        return req.Id;
    }

    private async Task<Guid> SeedRequestAsync(
        MeetingRequestStatus status, DateTimeOffset? slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK" + suffix,
            Name = "Speaker", NameArabic = "متحدّث",
            IsActive = true, AllowsMeetingRequests = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Requester", Subject = "Topic",
            Status = status,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart?.AddMinutes(30),
            RespondedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SpeakerMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }
}
