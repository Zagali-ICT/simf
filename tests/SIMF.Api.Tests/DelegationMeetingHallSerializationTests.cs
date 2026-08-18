// The delegation respond path used to bind a hall slot with no transaction at all: it
// read hall availability with no locks held, then saved. Its two siblings
// (SpeakerMeetingRequestService, BusinessMeetingService) already ran their scan-and-write
// inside CreateExecutionStrategy() + Serializable, so a delegation approve was the one
// hall writer that could not serialize against the other two. The filtered-unique
// (HallId, SlotStart) index could not cover the gap either: it lives on
// DelegationMeetingRequests alone (so it never sees a speaker row) and it keys on
// SlotStart (so it catches only EQUAL starts, not a 10:00-10:30 against a 10:15-10:45).
//
// The cases below pin the sequential cross-family outcome in both shapes -- the equal
// start and the partly-overlapping start -- so the refusal cannot regress into a
// double-booking, plus the happy path that proves the new transaction does not
// over-reject and still mints exactly one confirm token. The genuinely CONCURRENT race
// cannot be forced deterministically from an HTTP test; it is closed by construction,
// by running the same pattern the two green siblings use.
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
public sealed class DelegationMeetingHallSerializationTests : IClassFixture<SimfApiFactory>
{
    /// <summary>A far-future anchor for every fixture in this class, so the "slot is in
    /// the past" guard never fires and the dates cannot collide with another suite's.</summary>
    private static readonly DateTime SlotAnchor = new(2049, 6, 7, 9, 0, 0);

    /// <summary>The nvarchar(2000) ResponseNote column plus one character.</summary>
    private const int OverLongNoteLength = 2001;

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationMeetingHallSerializationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_approve_onto_a_slot_a_speaker_meeting_already_holds_is_refused()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();
        await SeedSpeakerMeetingHoldingHallAsync(hallId, SlotAnchor, SlotAnchor.AddHours(1));
        var requestId = await SeedPendingDelegationRequestAsync();

        // Exactly the slot the speaker meeting holds. This is the shape the filtered
        // unique index looks like it covers and does not: the index is delegation-only.
        var clash = await RespondAsync(requestId, admin, new RespondToDelegationMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Accepted,
            HallId = hallId,
            SlotStart = SlotAnchor,
            SlotEnd = SlotAnchor.AddHours(1),
        });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);

        // The refusal must leave the request untouched, not half-bound.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, stored.Status);
        Assert.Null(stored.HallId);
        Assert.Null(stored.RespondedAt);
    }

    [Fact]
    public async Task An_approve_onto_a_slot_a_speaker_meeting_partly_overlaps_is_refused()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();
        // 09:15-10:15 against the 09:00-10:00 slot the admin picks: the starts differ, so
        // a (HallId, SlotStart) index could never catch it even within one family.
        await SeedSpeakerMeetingHoldingHallAsync(
            hallId, SlotAnchor.AddMinutes(15), SlotAnchor.AddMinutes(75));
        var requestId = await SeedPendingDelegationRequestAsync();

        var clash = await RespondAsync(requestId, admin, new RespondToDelegationMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Accepted,
            HallId = hallId,
            SlotStart = SlotAnchor,
            SlotEnd = SlotAnchor.AddHours(1),
        });

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);

        // The third slot (11:00-12:00) clears the speaker's window entirely, so the
        // serialized unit must still take it -- the guard refuses an overlap, not the hall.
        var free = await RespondAsync(requestId, admin, new RespondToDelegationMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Accepted,
            HallId = hallId,
            SlotStart = SlotAnchor.AddHours(2),
            SlotEnd = SlotAnchor.AddHours(3),
        });
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
    }

    [Fact]
    public async Task An_approve_binds_the_hall_and_mints_exactly_one_confirm_token()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedMeetingHallAsync();
        var requestId = await SeedPendingDelegationRequestAsync();

        var approve = await RespondAsync(requestId, admin, new RespondToDelegationMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Accepted,
            HallId = hallId,
            SlotStart = SlotAnchor,
            SlotEnd = SlotAnchor.AddHours(1),
            ResponseNote = "Confirmed with both delegations.",
        });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, stored.Status);
        Assert.Equal(hallId, stored.HallId);
        Assert.Equal(SlotAnchor, stored.SlotStart);

        // The confirm token is staged OUTSIDE the retryable block precisely so a
        // serialization retry re-commits the same row instead of minting a second live
        // link into the target delegation's inbox. One approve, one token.
        var tokens = await db.DelegationMeetingActionTokens.AsNoTracking()
            .CountAsync(t => t.DelegationMeetingRequestId == requestId);
        Assert.Equal(1, tokens);
    }

    [Fact]
    public async Task An_over_length_response_note_is_refused_as_a_bilingual_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var requestId = await SeedPendingDelegationRequestAsync();

        // ResponseNote maps to nvarchar(2000) and nothing validated it, so an over-long
        // note reached SaveChanges and surfaced as a 500 truncation. The respond path now
        // catches DbUpdateException and reports a hall conflict, which would have made
        // this a misleading 409 on a Cancel that binds no slot at all.
        var tooLong = await RespondAsync(requestId, admin, new RespondToDelegationMeetingRequestRequest
        {
            Status = MeetingRequestStatus.Rejected,
            ResponseNote = new string('n', OverLongNoteLength),
        });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var body = (await tooLong.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Pending, stored.Status);
        Assert.Null(stored.ResponseNote);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>An active meeting hall with one availability window offering three free
    /// 60-minute slots from <see cref="SlotAnchor"/>. Fresh per test, so no other class
    /// (or case) shares its (HallId, SlotStart) index space.</summary>
    private async Task<Guid> SeedMeetingHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "DHS" + suffix,
            Name = "Serialization Hall", NameArabic = "قاعة اجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 20, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        db.HallAvailabilityWindows.Add(new HallAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            Start = SlotAnchor,
            End = SlotAnchor.AddHours(3),
            SlotMinutes = 60,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });

        await db.SaveChangesAsync();
        return hall.Id;
    }

    /// <summary>A LIVE (Accepted) SPEAKER meeting holding the given hall window. It is the
    /// other family entirely -- no delegation index and no delegation scan can see it, so
    /// only the shared hall-availability read refuses a delegation bind onto it.</summary>
    private async Task SeedSpeakerMeetingHoldingHallAsync(
        Guid hallId, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "DHS-" + suffix,
            Name = "Cross Family Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            Email = "speaker@simf.test",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);

        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Cross Family Requester",
            Subject = "Holds the hall slot",
            Status = MeetingRequestStatus.Accepted,
            HallId = hallId,
            SlotStart = start,
            SlotEnd = end,
            RespondedAt = SimfClock.Now,
            CreatedAt = SimfClock.Now,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>A Pending delegation request between two invited fixture delegations,
    /// seeded straight into the database so the case under test is the RESPOND path and
    /// not the submit path's own availability rules. The requester is a real approved
    /// visitor, because a successful respond resolves them for the outcome email.</summary>
    private async Task<Guid> SeedPendingDelegationRequestAsync()
    {
        var requesterId = await SeedVisitorAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var request = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = requesterId,
            RequestingCountryId = await EnsureCountryAsync(db, "ZY", 9401),
            TargetCountryId = await EnsureCountryAsync(db, "ZZ", 9402),
            AttendeeCount = 3,
            Subject = "Cross-family hall probe",
            Status = MeetingRequestStatus.Pending,
            CreatedAt = SimfClock.Now,
        };
        db.DelegationMeetingRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
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
        var email = $"dhs-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Cross Family Delegate",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"dhs-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "DHS Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> RespondAsync(
        Guid requestId, string token, RespondToDelegationMeetingRequestRequest body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
