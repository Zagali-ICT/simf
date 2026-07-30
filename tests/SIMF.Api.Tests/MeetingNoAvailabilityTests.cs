// Tests: G3 (owner 2026-07-30) — a meeting request CANNOT be sent when the target
// has no free availability slot. This SUPERSEDES rule R1 (D-767), which explicitly
// allowed a "legacy topic-only request" against a target with no windows at all.
// Both flows are covered (speaker + delegation) and both reasons an availability
// list can come back empty: no active future window, and every slot already taken.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// G3 — the submit-time availability guard on both meeting flows. Every window used
/// here is far in the future (2035) so "past slot" is never the reason a list is
/// empty, and every country code is class-local so a shared-database sibling test
/// can never seed or consume this class's windows.
/// </summary>
public sealed class MeetingNoAvailabilityTests : IClassFixture<SimfApiFactory>
{
    private static readonly DateTimeOffset WindowStart =
        new(2035, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingNoAvailabilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- speaker flow ---------------------------------------------------------

    [Fact]
    public async Task Speaker_with_no_availability_windows_is_409_no_availability()
    {
        var speakerId = await SeedSpeakerAsync();
        var requester = await SignInEligibleVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Captain Ahmed",
                Subject = "Naval cybersecurity",
            },
            requester);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingNoAvailability, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        // Nothing was persisted — the guard runs before the request row is written.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(
            await db.SpeakerMeetingRequests.AnyAsync(r => r.SpeakerId == speakerId));
    }

    [Fact]
    public async Task Speaker_whose_only_window_is_fully_taken_is_409_no_availability()
    {
        // One 30-minute window offering exactly one slot, already held by an
        // Accepted meeting → GetAvailableSlotsAsync returns empty for the SECOND
        // reason (taken, not missing). The guard must treat both identically.
        var speakerId = await SeedSpeakerAsync();
        await AddSpeakerWindowAsync(speakerId, WindowStart, minutes: 30, slotMinutes: 30);
        await TakeSpeakerSlotAsync(speakerId, WindowStart, WindowStart.AddMinutes(30));

        var requester = await SignInEligibleVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Captain Ahmed",
                Subject = "Naval cybersecurity",
            },
            requester);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingNoAvailability, body.Error!.Code);
    }

    [Fact]
    public async Task Speaker_with_a_free_slot_still_accepts_the_request()
    {
        var speakerId = await SeedSpeakerAsync();
        await AddSpeakerWindowAsync(speakerId, WindowStart, minutes: 60, slotMinutes: 30);
        var requester = await SignInEligibleVisitorAsync();

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Captain Ahmed",
                Subject = "Naval cybersecurity",
                SlotStart = WindowStart,
                SlotEnd = WindowStart.AddMinutes(30),
            },
            requester);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submitted = (await response.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Pending, submitted.Status);
    }

    // -- delegation flow ------------------------------------------------------

    [Fact]
    public async Task Delegation_with_no_availability_windows_is_409_no_availability()
    {
        var homeId = await EnsureCountryAsync("QG", 9301, invited: true);
        await EnsureCountryAsync("QH", 9302, invited: true);
        var requester = await CreateDelegateAsync(homeId);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "QH",
                AttendeeCount = 6,
                Subject = "Naval cooperation talks",
            },
            requester);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingNoAvailability, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.DelegationMeetingRequests
            .AnyAsync(r => r.RequestingCountryId == homeId));
    }

    [Fact]
    public async Task Delegation_whose_only_window_is_fully_taken_is_409_no_availability()
    {
        var homeId = await EnsureCountryAsync("QJ", 9303, invited: true);
        var targetId = await EnsureCountryAsync("QK", 9304, invited: true);
        var thirdId = await EnsureCountryAsync("QL", 9305, invited: true);
        await AddDelegationWindowAsync(
            targetId, WindowStart, minutes: 30, slotMinutes: 30);
        await TakeDelegationSlotAsync(
            thirdId, targetId, WindowStart, WindowStart.AddMinutes(30));

        var requester = await CreateDelegateAsync(homeId);
        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "QK",
                AttendeeCount = 6,
                Subject = "Naval cooperation talks",
            },
            requester);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingNoAvailability, body.Error!.Code);
    }

    [Fact]
    public async Task Delegation_with_a_free_slot_still_accepts_the_request()
    {
        var homeId = await EnsureCountryAsync("QM", 9306, invited: true);
        var targetId = await EnsureCountryAsync("QN", 9307, invited: true);
        await AddDelegationWindowAsync(
            targetId, WindowStart, minutes: 60, slotMinutes: 30);
        var requester = await CreateDelegateAsync(homeId);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "QN",
                AttendeeCount = 6,
                Subject = "Naval cooperation talks",
                SlotStart = WindowStart,
                SlotEnd = WindowStart.AddMinutes(30),
            },
            requester);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests
            .SingleAsync(r => r.TargetCountryId == targetId
                && r.RequestingCountryId == homeId);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<Guid> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "G3-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. G3 Speaker", NameArabic = "متحدّث",
            Email = $"g3-speaker-{Guid.NewGuid():N}@simf.test",
            AllowsMeetingRequests = true,
            IsActive = true,
            DisplayOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task AddSpeakerWindowAsync(
        Guid speakerId, DateTimeOffset start, int minutes, int slotMinutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            Start = start,
            End = start.AddMinutes(minutes),
            SlotMinutes = slotMinutes,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>An Accepted meeting holding [start, end) — Accepted is in
    /// <see cref="MeetingRequestStatuses.SlotHolding"/>, so the availability layer
    /// stops offering that slot.</summary>
    private async Task TakeSpeakerSlotAsync(
        Guid speakerId, DateTimeOffset start, DateTimeOffset end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Earlier Requester",
            Subject = "Already booked",
            SlotStart = start,
            SlotEnd = end,
            Status = MeetingRequestStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task AddDelegationWindowAsync(
        int countryId, DateTimeOffset start, int minutes, int slotMinutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.DelegationAvailabilityWindows.Add(new DelegationAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            CountryId = countryId,
            Start = start,
            End = start.AddMinutes(minutes),
            SlotMinutes = slotMinutes,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task TakeDelegationSlotAsync(
        int requestingCountryId, int targetCountryId,
        DateTimeOffset start, DateTimeOffset end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = requestingCountryId,
            TargetCountryId = targetCountryId,
            AttendeeCount = 4,
            Subject = "Already booked",
            SlotStart = start,
            SlotEnd = end,
            Status = MeetingRequestStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> EnsureCountryAsync(string code, int id, bool invited)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Countries.Add(country);
        }
        country.IsActive = true;
        country.IsInvited = invited;
        await db.SaveChangesAsync();
        return country.Id;
    }

    /// <summary>An approved visitor carrying the per-user AllowsSpeakerMeeting flag,
    /// so the eligibility gate is never the reason a submit is refused here.</summary>
    private async Task<string> SignInEligibleVisitorAsync()
    {
        var email = $"g3-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "G3 Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var type = await EnsureVisitorProfileTypeAsync(appDb);
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = type.Id,
                Name = user.DisplayName, NameArabic = "زائر",
                AllowsSpeakerMeeting = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }
        return await SignInAsync(email);
    }

    /// <summary>An approved delegate of <paramref name="nationalityId"/> carrying the
    /// per-user AllowsDelegationMeeting flag.</summary>
    private async Task<string> CreateDelegateAsync(int nationalityId)
    {
        var email = $"g3-delegate-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "G3 Delegate",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var type = await EnsureVisitorProfileTypeAsync(appDb);
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = type.Id,
                Name = user.DisplayName, NameArabic = "مندوب",
                NationalityId = nationalityId,
                IsDelegate = true,
                AllowsDelegationMeeting = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }
        return await SignInAsync(email);
    }

    private static async Task<UserProfileType> EnsureVisitorProfileTypeAsync(
        SimfAppDbContext appDb)
    {
        var type = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
        if (type is not null)
        {
            return type;
        }
        type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — G3Seed", NameArabic = "زائر",
            PageColor = "#3B82F6", IsForVisitor = true, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(type);
        await appDb.SaveChangesAsync();
        return type;
    }

    private async Task<string> SignInAsync(string email)
    {
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
