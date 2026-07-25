// Tests: D-474 (#11, Group G phase 1b) — VIP slot meeting requests + email/notify.
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
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Contracts.UserProfile;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-474 (#11, Group G phase 1b) — a VIP picks a free slot from a speaker's
/// availability windows; only VIP/VVIP may book a slot; the team accepts and the
/// requester is notified in-app (the speaker is emailed — async, not asserted here).
/// </summary>
public sealed class SpeakerMeetingVipSlotTests : IClassFixture<SimfApiFactory>
{
    private static readonly DateTimeOffset WindowStart = new(2030, 2, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerMeetingVipSlotTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_vip_books_a_free_slot_and_the_request_is_pending_with_the_slot()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vip: true);

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), vip);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.SingleAsync(r => r.SpeakerId == speakerId);
        Assert.Equal(WindowStart, req.SlotStart);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
        // D-612 — the picked slot's availability window is persisted (was inert).
        Assert.NotNull(req.AvailabilityWindowId);
    }

    [Fact]
    public async Task A_non_vip_booking_a_slot_is_403()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (plain, _) = await CreateVisitorAsync(vip: false);

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), plain);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_non_vip_topic_only_request_is_403()
    {
        // D-729 (owner item 15) — requesting a speaker meeting is now VIP-only,
        // even a topic-only request (no slot): a non-VIP is rejected up front.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (plain, _) = await CreateVisitorAsync(vip: false);

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Plain Visitor",
                Subject = "Topic-only meeting",
            },
            plain);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_vip_topic_only_request_is_ok()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vip: true);

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "VIP Guest",
                Subject = "Topic-only meeting",
            },
            vip);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_user_profile_read_reports_IsVip_from_the_tier()
    {
        // D-729 (owner item 15) — UserProfileResponse.IsVip mirrors the assigned
        // tier's AllowsVipMeetingSlots; the app uses it to gate the speaker CTA.
        var (vip, _) = await CreateVisitorAsync(vip: true);
        var (plain, _) = await CreateVisitorAsync(vip: false);

        Assert.True(await ReadIsVipAsync(vip));
        Assert.False(await ReadIsVipAsync(plain));
    }

    private async Task<bool> ReadIsVipAsync(string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/app/account/user-profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        return body.Data!.IsVip;
    }

    [Fact]
    public async Task A_slot_that_is_not_available_is_409()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vip: true);

        // A misaligned slot (not one the scheduler offers) → not available.
        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart.AddMinutes(5), WindowStart.AddMinutes(35)), vip);

        Assert.Equal((HttpStatusCode)409, response.StatusCode);
    }

    [Fact]
    public async Task Accepting_a_vip_slot_request_notifies_the_requester()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, vipUserId) = await CreateVisitorAsync(vip: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), vip);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!.Id;

        var respond = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{requestId}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        // The requester got an in-app notification (the speaker email is async/best-effort).
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var notified = await identity.Notifications.AnyAsync(n => n.UserId == vipUserId);
        Assert.True(notified);
    }

    [Fact]
    public async Task Accepting_a_second_request_for_an_already_accepted_slot_is_409()
    {
        // A1 — the accept-time slot re-check prevents double-booking: two VIPs may
        // both hold Pending requests for one slot (only Accepted excludes it), but
        // the second Accept is rejected.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip1, _) = await CreateVisitorAsync(vip: true);
        var (vip2, _) = await CreateVisitorAsync(vip: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit1 = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), vip1);
        var submit2 = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), vip2);
        Assert.Equal(HttpStatusCode.OK, submit1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submit2.StatusCode);
        var id1 = (await submit1.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!.Id;
        var id2 = (await submit2.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!.Id;

        var accept1 = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{id1}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, accept1.StatusCode);

        var accept2 = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{id2}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.Conflict, accept2.StatusCode);
        var body = (await accept2.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Accepting_a_second_request_whose_slot_overlaps_an_accepted_one_is_409()
    {
        // A1 (review) — overlapping availability windows with different slot lengths
        // can offer accepted slots that overlap but start at different times; the
        // accept-time guard must use half-open overlap, not start-equality.
        var winAStart = new DateTimeOffset(2030, 3, 1, 9, 0, 0, TimeSpan.Zero);   // [09:00,10:00]
        var winBStart = new DateTimeOffset(2030, 3, 1, 9, 30, 0, TimeSpan.Zero);  // [09:30,10:30]
        var speakerId = await SeedSpeakerWithTwoOverlappingWindowsAsync(winAStart, winBStart);
        var (vip1, _) = await CreateVisitorAsync(vip: true);
        var (vip2, _) = await CreateVisitorAsync(vip: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit1 = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(winAStart, winAStart.AddMinutes(60)), vip1);
        var submit2 = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(winBStart, winBStart.AddMinutes(60)), vip2);
        Assert.Equal(HttpStatusCode.OK, submit1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submit2.StatusCode);
        var id1 = (await submit1.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!.Id;
        var id2 = (await submit2.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!.Id;

        var accept1 = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{id1}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, accept1.StatusCode);

        // [09:30,10:30] overlaps the accepted [09:00,10:00] → rejected.
        var accept2 = await PostAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{id2}/respond",
            new RespondToSpeakerMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.Conflict, accept2.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<Guid> SeedSpeakerWithTwoOverlappingWindowsAsync(
        DateTimeOffset winAStart, DateTimeOffset winBStart)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Overlap Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        // Two overlapping 60-minute windows, each offering a single 60-minute slot
        // that starts at a different time but overlaps the other.
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(), SpeakerId = speaker.Id,
            Start = winAStart, End = winAStart.AddMinutes(60), SlotMinutes = 60,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        });
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(), SpeakerId = speaker.Id,
            Start = winBStart, End = winBStart.AddMinutes(60), SlotMinutes = 60,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private static SubmitSpeakerMeetingRequestRequest SlotRequest(
        DateTimeOffset start, DateTimeOffset end) =>
        new()
        {
            RequesterName = "VIP Guest",
            Subject = "Partnership discussion",
            SlotStart = start,
            SlotEnd = end,
        };

    private async Task<Guid> SeedSpeakerWithWindowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Test Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            // Inline email (was a linked Contact) — the meeting-accept email path.
            Email = "speaker@simf.test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            Start = WindowStart,
            End = WindowStart.AddMinutes(60),
            SlotMinutes = 30,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task<(string Token, Guid UserId)> CreateVisitorAsync(bool vip)
    {
        var email = $"smr-{(vip ? "vip" : "plain")}-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        Guid profileTypeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = vip ? "VIP Guest" : "Plain Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var typeName = vip ? "VIP" : "Normal";
            var type = await appDb.ProfileTypes.FirstOrDefaultAsync(p => p.Name == typeName);
            if (type is null)
            {
                type = new UserProfileType
                {
                    Id = Guid.NewGuid(),
                    Name = typeName, NameArabic = typeName,
                    PageColor = "#FFD700", IsForVisitor = true, IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                appDb.ProfileTypes.Add(type);
            }
            profileTypeId = type.Id;
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = profileTypeId,
                // Bi-Meeting rework — eligibility is now the per-user flag, not the tier.
                AllowsSpeakerMeeting = vip,
                Name = user.DisplayName, NameArabic = user.DisplayName,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, userId);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"smr-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "SMR Admin",
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
        string url, TBody body, string token, HttpMethod? method = null)
        where TBody : class
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
