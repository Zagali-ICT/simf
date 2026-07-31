// Tests: D-773 — the shared MeetingTableOverlapGuard.
//
// A34 (D-771) + D3 (D-772) put a three-family table-overlap scan on the DELEGATION
// bind only, so the "a meeting table holds one meeting at a time" invariant was
// one-directional: the delegation desk was protected, the other two binds were not.
//   * SpeakerMeetingRequestService.BindHallSlotAsync assigned MeetingTableId after
//     only an "active + in this hall" check — no overlap scan of any family.
//   * BusinessMeetingService.ScheduleMeetingAsync scanned its own family only.
// These three cases pin the invariant in both remaining directions and prove the
// guard does not over-reject a touching (half-open, non-overlapping) window.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MeetingTableOverlapTests : IClassFixture<SimfApiFactory>
{
    /// <summary>A future anchor for the two request-family binds (they are bound only
    /// by "not in the past" plus the hall availability window). Deliberately far past
    /// the forum days so it cannot collide with any other suite's fixtures.</summary>
    private static readonly DateTime RequestAnchor =
        new(2048, 5, 4, 9, 0, 0);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingTableOverlapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_speaker_bind_onto_a_table_held_by_a_delegation_meeting_is_refused()
    {
        var slot = RequestAnchor;
        var admin = await CreateAdministratorAndSignInAsync();
        var (hallId, tableId) = await SeedHallWithTableAsync(slot);
        await SeedDelegationMeetingHoldingTableAsync(tableId, slot, slot.AddHours(1));
        var requestId = await SeedPendingSpeakerRequestAsync();

        // Hall B is free at the hall level (the delegation meeting sits in its own
        // hall), so ONLY a table-level scan of the delegation family can catch this.
        var clash = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{requestId}/respond", admin,
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStart = slot,
                SlotEnd = slot.AddHours(1),
                MeetingTableId = tableId,
            });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);

        // The refusal must leave the request untouched, not half-bound.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
        Assert.Null(req.MeetingTableId);
    }

    [Fact]
    public async Task A_business_meeting_onto_a_request_held_table_is_refused()
    {
        var slot = await ForumSlotAsync(hourOffset: 2);
        var admin = await CreateAdministratorAndSignInAsync();
        var (_, tableId) = await SeedHallWithTableAsync(slot);
        await SeedDelegationMeetingHoldingTableAsync(tableId, slot, slot.AddHours(1));

        // Overlapping by 30 minutes — the business family's own scan sees nothing,
        // because the holder is a delegation meeting request.
        var clash = await PostAuthAsync(
            "/api/v1/admin/business-meetings", admin,
            await ScheduleBodyAsync(tableId, slot.AddMinutes(30), slot.AddMinutes(90)));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingTableConflict, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.BusinessMeetings.AsNoTracking()
            .AnyAsync(m => m.MeetingTableId == tableId));
    }

    [Fact]
    public async Task A_speaker_bind_on_the_same_table_in_a_touching_slot_still_succeeds()
    {
        var slot = RequestAnchor.AddHours(4);
        var admin = await CreateAdministratorAndSignInAsync();
        var (hallId, tableId) = await SeedHallWithTableAsync(slot);
        await SeedDelegationMeetingHoldingTableAsync(tableId, slot, slot.AddHours(1));
        var requestId = await SeedPendingSpeakerRequestAsync();

        // 10:00-11:00 against a 09:00-10:00 booking — half-open overlap, so the two
        // touch but do not collide. The guard must not over-reject this.
        var bind = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{requestId}/respond", admin,
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStart = slot.AddHours(1),
                SlotEnd = slot.AddHours(2),
                MeetingTableId = tableId,
            });

        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(tableId, req.MeetingTableId);
        Assert.Equal(slot.AddHours(1), req.SlotStart);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A meeting hall with an availability window offering three free
    /// 60-minute slots from <paramref name="slotStart"/>, plus one active table. The
    /// hall itself holds nothing, so only a TABLE-level scan can refuse a bind.</summary>
    private async Task<(Guid HallId, Guid TableId)> SeedHallWithTableAsync(
        DateTime slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MTO" + suffix,
            Name = "Meeting Hall", NameArabic = "قاعة اجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 20, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        var table = new MeetingTable
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Code = "TT" + suffix,
            Capacity = 6, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.MeetingTables.Add(table);

        db.HallAvailabilityWindows.Add(new HallAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Start = slotStart,
            End = slotStart.AddHours(3),
            SlotMinutes = 60,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });

        await db.SaveChangesAsync();
        return (hall.Id, table.Id);
    }

    /// <summary>A LIVE (Accepted) delegation meeting that holds
    /// <paramref name="tableId"/> over the given window, placed in a DIFFERENT hall so
    /// no hall-level guard sees it — the table is the only shared resource.</summary>
    private async Task SeedDelegationMeetingHoldingTableAsync(
        Guid tableId, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var otherHall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MTH" + suffix,
            Name = "Holder Hall", NameArabic = "قاعة",
            Purpose = HallPurpose.Meeting, Capacity = 20, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(otherHall);

        db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = await EnsureCountryAsync(db, "ZM", 9301),
            TargetCountryId = await EnsureCountryAsync(db, "ZN", 9302),
            AttendeeCount = 2,
            Subject = "Holds the table",
            Status = MeetingRequestStatus.Accepted,
            HallId = otherHall.Id,
            MeetingTableId = tableId,
            SlotStart = start,
            SlotEnd = end,
            RespondedAt = SimfClock.Now,
            CreatedAt = SimfClock.Now,
        });

        await db.SaveChangesAsync();
    }

    private static async Task<int> EnsureCountryAsync(
        SimfAppDbContext db, string code, int id)
    {
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                IsActive = true, IsInvited = true, CreatedAt = SimfClock.Now,
            };
            db.Countries.Add(country);
            await db.SaveChangesAsync();
        }
        return country.Id;
    }

    /// <summary>A Pending speaker meeting request whose requester is a real approved
    /// visitor (the respond path resolves them for the outcome notification).</summary>
    private async Task<Guid> SeedPendingSpeakerRequestAsync()
    {
        var requesterId = await SeedVisitorAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "MTO-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Table Guard Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            Email = "speaker@simf.test",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);

        var request = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = requesterId,
            RequesterName = "Table Guard Requester",
            Subject = "Table guard probe",
            Status = MeetingRequestStatus.Pending,
            CreatedAt = SimfClock.Now,
        };
        db.SpeakerMeetingRequests.Add(request);

        await db.SaveChangesAsync();
        return request.Id;
    }

    /// <summary>A business-meeting body with two fresh companies, so a participant
    /// conflict can never be the reason a schedule call is refused.</summary>
    private async Task<ScheduleMeetingRequest> ScheduleBodyAsync(
        Guid tableId, DateTime start, DateTime end) =>
        new()
        {
            MeetingTableId = tableId,
            MeetingType = BusinessMeetingType.B2B,
            Start = start,
            End = end,
            Participants =
            [
                new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
            ],
        };

    /// <summary>D-753 bounds a business meeting to the authored forum days, so the
    /// slot is anchored to the FIRST active programme day (09:00 event-local) rather
    /// than to a hard-coded date that drifts when the content seed changes.</summary>
    private async Task<DateTime> ForumSlotAsync(int hourOffset)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var firstDay = await db.ProgrammeDays.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Date)
            .Select(d => (DateOnly?)d.Date)
            .FirstOrDefaultAsync();
        Assert.True(firstDay.HasValue,
            "The test fixture must seed programme days for the forum-day bound.");
        return firstDay!.Value.ToDateTime(new TimeOnly(9, 0))
            .AddHours(hourOffset);
    }

    private async Task<Guid> SeedCompanyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = $"Co {Guid.NewGuid():N}",
            NameArabic = "شركة",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Exhibitors.Add(exhibitor);
        await db.SaveChangesAsync();
        return exhibitor.Id;
    }

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"mto-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Table Guard Requester",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"mto-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "MTO Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, string token, TBody body)
        where TBody : class =>
        SendAuthAsync(HttpMethod.Post, url, token, body);

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, string token, TBody body)
        where TBody : class =>
        SendAuthAsync(HttpMethod.Put, url, token, body);

    private Task<HttpResponseMessage> SendAuthAsync<TBody>(
        HttpMethod method, string url, string token, TBody body)
        where TBody : class
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
