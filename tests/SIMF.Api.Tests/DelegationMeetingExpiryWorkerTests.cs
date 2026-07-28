// Tests: B10 — MeetingAwaitingSpeakerExpiryWorker.RunDelegationExpiryScanAsync.
// Nothing used to expire an unconfirmed DELEGATION meeting, yet AwaitingSpeaker is a
// slot-holding state, so one that the target delegation never confirmed held its hall
// slot forever. The sweep now reverts it to a clean Pending (which frees the slot) once
// its 72h confirm token can no longer be used; a request with a live token, and any
// decided request, are left untouched.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DelegationMeetingExpiryWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public DelegationMeetingExpiryWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Reverts_an_awaiting_delegation_meeting_whose_confirm_token_expired()
    {
        var now = DateTimeOffset.UtcNow;
        var requestId = await SeedBoundAwaitingRequestAsync(
            slotStart: now.AddDays(3), tokenExpires: now.AddHours(-1), tokenUsed: false);

        var reverted = await RunScanAsync(now);
        Assert.Equal(1, reverted);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
        Assert.Null(req.HallId);
        Assert.Null(req.MeetingTableId);
        Assert.Null(req.SlotStart);
        Assert.Null(req.SlotEnd);
        Assert.Null(req.RespondedAt);
        Assert.Null(req.RespondedByUserId);
        Assert.Null(req.ResponseNote);

        var audited = await db.OperationLog.AnyAsync(e =>
            e.EventType == AuditEvents.DelegationMeetingRequestReverted
            && e.Detail!.Contains(requestId.ToString()));
        Assert.True(audited);
    }

    [Fact]
    public async Task Reverts_an_awaiting_delegation_meeting_whose_confirm_token_was_used()
    {
        // A consumed token is as dead as an expired one — but a used token means the
        // meeting already left AwaitingSpeaker in the real flow, so a row still sitting
        // there with a used token is stuck and must be handed back to the admin queue.
        var now = DateTimeOffset.UtcNow;
        var requestId = await SeedBoundAwaitingRequestAsync(
            slotStart: now.AddDays(3), tokenExpires: now.AddHours(48), tokenUsed: true);

        Assert.Equal(1, await RunScanAsync(now));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
    }

    [Fact]
    public async Task Leaves_an_awaiting_delegation_meeting_with_a_live_confirm_token()
    {
        var now = DateTimeOffset.UtcNow;
        var requestId = await SeedBoundAwaitingRequestAsync(
            slotStart: now.AddDays(3), tokenExpires: now.AddHours(48), tokenUsed: false);

        Assert.Equal(0, await RunScanAsync(now));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, req.Status);
        Assert.NotNull(req.SlotStart);
        Assert.NotNull(req.HallId);
    }

    [Fact]
    public async Task Leaves_decided_delegation_meetings_alone()
    {
        var now = DateTimeOffset.UtcNow;
        var acceptedId = await SeedRequestAsync(MeetingRequestStatus.Accepted, now.AddDays(3));
        var rejectedId = await SeedRequestAsync(MeetingRequestStatus.Rejected, null);

        Assert.Equal(0, await RunScanAsync(now));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(MeetingRequestStatus.Accepted,
            (await db.DelegationMeetingRequests.SingleAsync(r => r.Id == acceptedId)).Status);
        Assert.Equal(MeetingRequestStatus.Rejected,
            (await db.DelegationMeetingRequests.SingleAsync(r => r.Id == rejectedId)).Status);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<int> RunScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        return await MeetingAwaitingSpeakerExpiryWorker.RunDelegationExpiryScanAsync(
            db, auditLog, now, CancellationToken.None);
    }

    private async Task<Guid> SeedBoundAwaitingRequestAsync(
        DateTimeOffset slotStart, DateTimeOffset tokenExpires, bool tokenUsed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var (requesting, target) = await EnsureCountriesAsync(db);

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "DH" + suffix,
            Name = "Meeting Hall", NameArabic = "قاعة",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);

        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Code = "DT" + suffix,
            Capacity = 4, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MeetingTables.Add(table);

        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = requesting,
            TargetCountryId = target,
            AttendeeCount = 3,
            Subject = "Expiry sweep probe",
            Status = MeetingRequestStatus.AwaitingSpeaker,
            HallId = hall.Id,
            MeetingTableId = table.Id,
            SlotStart = slotStart,
            SlotEnd = slotStart.AddMinutes(30),
            RespondedAt = DateTimeOffset.UtcNow,
            RespondedByUserId = Guid.NewGuid(),
            ResponseNote = "bound",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.DelegationMeetingRequests.Add(req);

        db.DelegationMeetingActionTokens.Add(new DelegationMeetingActionToken
        {
            Id = Guid.NewGuid(),
            DelegationMeetingRequestId = req.Id,
            TokenHash = "hash-" + Guid.NewGuid().ToString("N"),
            ExpiresUtc = tokenExpires,
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
        var (requesting, target) = await EnsureCountriesAsync(db);
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = requesting,
            TargetCountryId = target,
            AttendeeCount = 2,
            Subject = "Decided probe",
            Status = status,
            SlotStart = slotStart,
            SlotEnd = slotStart?.AddMinutes(30),
            RespondedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.DelegationMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return req.Id;
    }

    // IT / PT keep clear of the codes the other delegation suites use.
    private static async Task<(int Requesting, int Target)> EnsureCountriesAsync(
        SimfAppDbContext db)
    {
        var requesting = await EnsureCountryAsync(db, "IT", 380);
        var target = await EnsureCountryAsync(db, "PT", 620);
        return (requesting, target);
    }

    private static async Task<int> EnsureCountryAsync(SimfAppDbContext db, string code, int id)
    {
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                IsActive = true, IsInvited = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Countries.Add(country);
            await db.SaveChangesAsync();
        }
        return country.Id;
    }
}
