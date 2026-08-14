// Tests: D-474 (#11, Group G phase 1b) — speaker-meeting eligibility + slot
// booking + email/notify. Was SpeakerMeetingVipSlotTests until D-760 moved the
// gate off the VIP tier.
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
/// D-474 (#11, Group G phase 1b) — an eligible requester picks a free slot from a
/// speaker's availability windows; the team accepts and the requester is notified
/// in-app (the speaker is emailed — async, not asserted here).
///
/// <para>The gate these tests exercise is the per-user
/// <c>UserProfile.AllowsSpeakerMeeting</c> flag. D-760 moved it off the VVIP/VIP
/// tier, which is why this class is no longer called <c>…VipSlotTests</c>.</para>
///
/// <para>The single-bool <c>CreateVisitorAsync(bool vipAndEligible)</c> sets the
/// profile TYPE and the per-user flag together, so a test using it cannot on its
/// own tell the two gates apart — it would still pass if the tier gate were
/// reinstated. The two tests named <c>*_pins_D760_*</c> use the two-argument
/// overload to set them APART, one per direction, which is what actually pins the
/// decoupling.</para>
/// </summary>
public sealed class SpeakerMeetingEligibilityAndSlotTests : IClassFixture<SimfApiFactory>
{
    private static readonly DateTime WindowStart = new(2030, 2, 1, 9, 0, 0);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerMeetingEligibilityAndSlotTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_eligible_requester_books_a_free_slot_and_the_request_is_pending()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vipAndEligible: true);

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
    public async Task An_ineligible_requester_booking_a_slot_is_403()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (plain, _) = await CreateVisitorAsync(vipAndEligible: false);

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart, WindowStart.AddMinutes(30)), plain);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_ineligible_topic_only_request_is_403()
    {
        // Requesting a speaker meeting needs the per-user AllowsSpeakerMeeting
        // flag (D-760, replacing the D-729 VIP-tier gate), even for a topic-only
        // request (no slot): an ineligible requester is rejected up front. This
        // helper sets the flag and the tier together, so vipAndEligible: false
        // means both are false; the two _pins_D760_ tests below separate them.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (plain, _) = await CreateVisitorAsync(vipAndEligible: false);

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
    public async Task An_eligible_topic_only_request_is_ok()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vipAndEligible: true);

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
    public async Task A_vip_tier_without_the_per_user_flag_pins_D760_and_is_403()
    {
        // D-760, direction 1 — the VVIP/VIP tier does NOT grant a speaker meeting.
        // This exact account WAS eligible under D-729, so the test fails if the
        // tier gate is ever reinstated. Topic-only, to isolate the eligibility
        // gate from the slot rules.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (token, userId) = await CreateVisitorAsync(
            vipTier: true, allowsSpeakerMeeting: false);

        Assert.True(
            await ReadTierIsVipAsync(userId),
            "the fixture must assign a genuine VIP tier, or this proves nothing");

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "VIP Guest",
                Subject = "Topic-only meeting",
            },
            token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_normal_tier_with_the_per_user_flag_pins_D760_and_is_ok()
    {
        // D-760, direction 2 — eligibility is admin-assigned per user, so a
        // Normal-tier attendee carrying the flag may request a meeting. This
        // account was refused under D-729.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (token, userId) = await CreateVisitorAsync(
            vipTier: false, allowsSpeakerMeeting: true);

        Assert.False(
            await ReadTierIsVipAsync(userId),
            "the fixture must assign a non-VIP tier, or this proves nothing");

        var response = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "Plain Visitor",
                Subject = "Topic-only meeting",
            },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary><c>IsVipTier</c> of the profile type assigned to
    /// <paramref name="userId"/> — the same hop
    /// <c>SeatReservationService.IsVipVisitorAsync</c> makes. The two D-760 tests
    /// assert it so that a fixture which stopped setting the tier would fail loudly
    /// instead of quietly turning them into duplicates of the tests above.</summary>
    private async Task<bool> ReadTierIsVipAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId && p.ProfileTypeId != null)
            .Join(appDb.ProfileTypes.AsNoTracking(),
                p => p.ProfileTypeId, t => (Guid?)t.Id, (p, t) => t.IsVipTier)
            .FirstOrDefaultAsync();
    }

    [Fact]
    public async Task The_user_profile_read_reports_IsVip_from_the_tier()
    {
        // D-729 (owner item 15) — UserProfileResponse.IsVip mirrors the assigned
        // tier's IsVipTier. It no longer gates the speaker CTA: D-760
        // moved that to the per-user AllowsSpeakerMeeting. The field is still
        // served, and the tier itself still decides VIP-tier seat self-reservation
        // (SeatReservationService.IsVipVisitorAsync), so this stays covered.
        var (vip, _) = await CreateVisitorAsync(vipAndEligible: true);
        var (plain, _) = await CreateVisitorAsync(vipAndEligible: false);

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
    public async Task A_slot_is_accepted_without_an_availability_recheck_and_resolves_its_window()
    {
        // Was `A_slot_that_is_not_available_is_409` (asserting 409 on a slot the
        // scheduler does not offer). Rule R1 of the D-767 bi-meeting rework
        // deliberately DROPPED the submit-time availability re-check: several
        // Pending requests may target the same time, the admin approves exactly one
        // under the Serializable approve guard, and a reserved slot is already
        // hidden from the picker (R2) — so a requester never sees a same-time
        // error. Submit now validates only the slot PAIR (both-or-neither,
        // end > start) and resolves which window the slot came from.
        //
        // This asserts that current contract on both sides of the resolve-by-range
        // lookup, which is the behaviour that actually still exists here.
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, _) = await CreateVisitorAsync(vipAndEligible: true);

        // Misaligned (09:05-09:35 against 30-minute slots) but still INSIDE the
        // 09:00-10:00 window: accepted, and linked to that window.
        var inside = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart.AddMinutes(5), WindowStart.AddMinutes(35)), vip);
        Assert.Equal(HttpStatusCode.OK, inside.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var request = await db.SpeakerMeetingRequests
            .SingleAsync(r => r.SpeakerId == speakerId);
        Assert.Equal(WindowStart.AddMinutes(5), request.SlotStart);
        Assert.NotNull(request.AvailabilityWindowId);

        // R8 — a second submission MOVES the same Pending request rather than
        // duplicating it. A slot beyond the window's end resolves to NO window, so
        // the link is cleared rather than left pointing at a window that does not
        // contain the slot.
        var outside = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            SlotRequest(WindowStart.AddHours(3), WindowStart.AddHours(3).AddMinutes(30)), vip);
        Assert.Equal(HttpStatusCode.OK, outside.StatusCode);

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var moved = await afterDb.SpeakerMeetingRequests
            .SingleAsync(r => r.SpeakerId == speakerId);
        Assert.Equal(WindowStart.AddHours(3), moved.SlotStart);
        Assert.Null(moved.AvailabilityWindowId);
    }

    [Fact]
    public async Task Accepting_a_slot_request_notifies_the_requester()
    {
        var speakerId = await SeedSpeakerWithWindowAsync();
        var (vip, vipUserId) = await CreateVisitorAsync(vipAndEligible: true);
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
        var (vip1, _) = await CreateVisitorAsync(vipAndEligible: true);
        var (vip2, _) = await CreateVisitorAsync(vipAndEligible: true);
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
        var winAStart = new DateTime(2030, 3, 1, 9, 0, 0);   // [09:00,10:00]
        var winBStart = new DateTime(2030, 3, 1, 9, 30, 0);  // [09:30,10:30]
        var speakerId = await SeedSpeakerWithTwoOverlappingWindowsAsync(winAStart, winBStart);
        var (vip1, _) = await CreateVisitorAsync(vipAndEligible: true);
        var (vip2, _) = await CreateVisitorAsync(vipAndEligible: true);
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
        DateTime winAStart, DateTime winBStart)
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
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        // Two overlapping 60-minute windows, each offering a single 60-minute slot
        // that starts at a different time but overlaps the other.
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(), SpeakerId = speaker.Id,
            Start = winAStart, End = winAStart.AddMinutes(60), SlotMinutes = 60,
            IsActive = true, CreatedAt = SimfClock.Now,
        });
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(), SpeakerId = speaker.Id,
            Start = winBStart, End = winBStart.AddMinutes(60), SlotMinutes = 60,
            IsActive = true, CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private static SubmitSpeakerMeetingRequestRequest SlotRequest(
        DateTime start, DateTime end) =>
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
            CreatedAt = SimfClock.Now,
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
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    /// <summary>The common case: VIP tier AND the per-user speaker-meeting flag
    /// move together. Every pre-D-760 test uses this, which is exactly why none of
    /// them can tell the two gates apart — see the two-argument overload.</summary>
    private Task<(string Token, Guid UserId)> CreateVisitorAsync(bool vipAndEligible) =>
        CreateVisitorAsync(
            vipTier: vipAndEligible, allowsSpeakerMeeting: vipAndEligible);

    /// <summary>Create an approved visitor with the VIP TIER
    /// (<c>ProfileType.IsVipTier</c>) and the per-user
    /// <c>UserProfile.AllowsSpeakerMeeting</c> flag set INDEPENDENTLY, which is
    /// what D-760 decoupled.
    ///
    /// <para>The tier flag is written here rather than left to whatever the seeder
    /// put on the row, so a test asserting "a genuine VIP tier is still refused"
    /// cannot pass vacuously against a fixture that quietly stopped being VIP. The
    /// value written always matches the tier the name implies (VIP row true, Normal
    /// row false), so sharing these seeded rows across tests stays safe.</para>
    /// </summary>
    private async Task<(string Token, Guid UserId)> CreateVisitorAsync(
        bool vipTier, bool allowsSpeakerMeeting)
    {
        var label = $"{(vipTier ? "vip" : "plain")}{(allowsSpeakerMeeting ? "-ok" : "")}";
        var email = $"smr-{label}-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        Guid profileTypeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = vipTier ? "VIP Guest" : "Plain Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var typeName = vipTier ? "VIP" : "Normal";
            var type = await appDb.ProfileTypes.FirstOrDefaultAsync(p => p.Name == typeName);
            if (type is null)
            {
                type = new UserProfileType
                {
                    Id = Guid.NewGuid(),
                    Name = typeName, NameArabic = typeName,
                    PageColor = "#FFD700", IsForVisitor = true, IsActive = true,
                    CreatedAt = SimfClock.Now,
                };
                appDb.ProfileTypes.Add(type);
            }
            type.IsVipTier = vipTier;
            profileTypeId = type.Id;
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = profileTypeId,
                // Bi-Meeting rework — eligibility is now the per-user flag, not the tier.
                AllowsSpeakerMeeting = allowsSpeakerMeeting,
                Name = user.DisplayName, NameArabic = user.DisplayName,
                CreatedAt = SimfClock.Now,
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
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
