// Tests: the shared MeetingRequesterOverlapGuard.
//
// "A meeting table holds one meeting at a time" already had a single cross-family
// authority (MeetingTableOverlapGuard). "A PERSON holds one meeting at a time" did
// not: each bind path scanned its own family only, so a VIP carrying both
// AllowsSpeakerMeeting and AllowsDelegationMeeting could be approved into a speaker
// meeting AND a delegation meeting at the same instant in two different halls.
//   * SpeakerMeetingRequestService's requester re-check scanned SpeakerMeetingRequests.
//   * DelegationMeetingRequestService's guard is keyed on COUNTRIES, so it cannot see
//     the requester's own speaker meeting at all.
// Neither hall nor table guard covers it either: the two meetings sit in different
// halls at different tables. The three cases below pin the invariant in both
// directions and prove the guard does not over-reject a touching window.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Meetings)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class MeetingRequesterOverlapTests : IClassFixture<SimfApiFactory>
{
    /// <summary>A far-future anchor for every fixture in this class, so the "slot is in
    /// the past" guard never fires and the dates cannot collide with another suite's.</summary>
    private static readonly DateTime SlotAnchor = new(2051, 7, 8, 9, 0, 0);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingRequesterOverlapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_speaker_approve_is_refused_when_the_requester_holds_a_delegation_meeting()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var requesterId = await SeedVisitorAsync();
        var hallId = await SeedMeetingHallWithWindowAsync(SlotAnchor);
        await SeedDelegationMeetingForRequesterAsync(
            requesterId, SlotAnchor, SlotAnchor.AddHours(1));
        var requestId = await SeedPendingSpeakerRequestAsync(requesterId);

        // The delegation meeting sits in its OWN hall at no table, so neither the hall
        // guard nor the table guard can see it: only a person-level scan of the other
        // family refuses this.
        var clash = await RespondAsync(
            $"/api/v1/admin/speaker-meeting-requests/{requestId}/respond", admin,
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStart = SlotAnchor,
                SlotEnd = SlotAnchor.AddHours(1),
            });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);

        // The refusal must leave the request untouched, not half-bound.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, stored.Status);
        Assert.Null(stored.HallId);
    }

    [Fact]
    public async Task A_delegation_approve_is_refused_when_the_requester_holds_a_speaker_meeting()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var requesterId = await SeedVisitorAsync();
        var slot = SlotAnchor.AddDays(1);
        var hallId = await SeedMeetingHallWithWindowAsync(slot);
        await SeedSpeakerMeetingForRequesterAsync(requesterId, slot, slot.AddHours(1));
        var requestId = await SeedPendingDelegationRequestAsync(requesterId);

        // The delegation guard is keyed on the two COUNTRIES, and the speaker meeting
        // has none, so before the shared guard this approve committed.
        var clash = await RespondAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond", admin,
            new RespondToDelegationMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStart = slot,
                SlotEnd = slot.AddHours(1),
            });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, stored.Status);
        Assert.Null(stored.HallId);
    }

    [Fact]
    public async Task A_speaker_approve_in_a_touching_slot_still_succeeds()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var requesterId = await SeedVisitorAsync();
        var slot = SlotAnchor.AddDays(2);
        var hallId = await SeedMeetingHallWithWindowAsync(slot);
        await SeedDelegationMeetingForRequesterAsync(requesterId, slot, slot.AddHours(1));
        var requestId = await SeedPendingSpeakerRequestAsync(requesterId);

        // 10:00-11:00 against a 09:00-10:00 booking — half-open overlap, so the two
        // touch but do not collide. The guard must not over-reject this.
        var bind = await RespondAsync(
            $"/api/v1/admin/speaker-meeting-requests/{requestId}/respond", admin,
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId,
                SlotStart = slot.AddHours(1),
                SlotEnd = slot.AddHours(2),
            });

        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(slot.AddHours(1), stored.SlotStart);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A meeting hall with an availability window offering three free
    /// 60-minute slots from <paramref name="slotStart"/>. The hall itself holds
    /// nothing, so only a person-level scan can refuse a bind into it.</summary>
    private async Task<Guid> SeedMeetingHallWithWindowAsync(DateTime slotStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = NewMeetingHall(db);
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
        return hall.Id;
    }

    /// <summary>A LIVE delegation meeting held BY <paramref name="requesterUserId"/>,
    /// in its own hall so no hall-level guard can see it.</summary>
    private async Task SeedDelegationMeetingForRequesterAsync(
        Guid requesterUserId, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var otherHall = NewMeetingHall(db);
        db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = await EnsureCountryAsync(db, "ZS", 9501),
            TargetCountryId = await EnsureCountryAsync(db, "ZT", 9502),
            AttendeeCount = 2,
            Subject = "Holds the requester",
            Status = MeetingRequestStatus.Accepted,
            HallId = otherHall.Id,
            SlotStart = start,
            SlotEnd = end,
            RespondedAt = SimfClock.Now,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>A LIVE speaker meeting held BY <paramref name="requesterUserId"/>,
    /// in its own hall so no hall-level guard can see it.</summary>
    private async Task SeedSpeakerMeetingForRequesterAsync(
        Guid requesterUserId, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var otherHall = NewMeetingHall(db);
        var speaker = NewSpeaker(db);
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = requesterUserId,
            RequesterName = "Requester Overlap",
            Subject = "Holds the requester",
            Status = MeetingRequestStatus.Accepted,
            HallId = otherHall.Id,
            SlotStart = start,
            SlotEnd = end,
            RespondedAt = SimfClock.Now,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedPendingSpeakerRequestAsync(Guid requesterUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = NewSpeaker(db);
        var request = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = requesterUserId,
            RequesterName = "Requester Overlap",
            Subject = "Requester guard probe",
            Status = MeetingRequestStatus.Pending,
            CreatedAt = SimfClock.Now,
        };
        db.SpeakerMeetingRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private async Task<Guid> SeedPendingDelegationRequestAsync(Guid requesterUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var request = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterUserId,
            RequestingCountryId = await EnsureCountryAsync(db, "ZS", 9501),
            TargetCountryId = await EnsureCountryAsync(db, "ZT", 9502),
            AttendeeCount = 3,
            Subject = "Requester guard probe",
            Status = MeetingRequestStatus.Pending,
            CreatedAt = SimfClock.Now,
        };
        db.DelegationMeetingRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private static Hall NewMeetingHall(SimfAppDbContext db)
    {
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MRO" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall", NameArabic = "قاعة اجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 20, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        return hall;
    }

    /// <summary>The speaker needs a contact email: the approve path refuses up front
    /// when the confirmation link could never be delivered.</summary>
    private static Speaker NewSpeaker(SimfAppDbContext db)
    {
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "MRO-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Requester Guard Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            Email = "speaker@simf.test",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        return speaker;
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

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"mro-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Requester Overlap",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"mro-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "MRO Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> RespondAsync<TBody>(
        string url, string token, TBody body)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
