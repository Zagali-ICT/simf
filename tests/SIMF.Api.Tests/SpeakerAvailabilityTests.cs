// Tests: D-474 (#11, Group G phase 1) — speaker availability windows + free slots.
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
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SpeakerAvailabilityTests : IClassFixture<SimfApiFactory>
{
    // D-753 — availability windows are bounded to the forum days (MIN/MAX over the
    // active ProgrammeDay rows). The fixture seeds programme days 2026-11-23..25, so the
    // window sits on day one — inside that window and (relative to the test clock) in the
    // future so the derived slots are still offered.
    //
    // Was 2026-11-20 until 2026-07-28: db9b6f76 moved the real SIMF-4 programme to
    // 23-25 Nov and soft-deletes the old 20-22 placeholder days, which put this
    // window outside the forum bound so the window was refused and no slot existed.
    private static readonly DateTime WindowStart = new(2026, 11, 23, 10, 0, 0);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerAvailabilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_window_then_it_lists_and_yields_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();

        // A 60-minute window with 30-minute slots → 2 slots.
        var create = await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var windows = await GetWindowsAsync(speakerId, admin);
        Assert.Single(windows);

        var slots = await GetSlotsAsync(speakerId, admin);
        Assert.Equal(2, slots.Count);
        Assert.Equal(WindowStart, slots[0].Start);
        Assert.Equal(WindowStart.AddMinutes(30), slots[0].End);
    }

    [Fact]
    public async Task An_accepted_meeting_slot_is_excluded_from_the_free_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);

        // Mark the first slot as taken by an accepted meeting.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
            {
                Id = Guid.NewGuid(),
                SpeakerId = speakerId,
                RequestedByUserId = Guid.NewGuid(),
                RequesterName = "VIP",
                Subject = "Slot taken",
                Status = MeetingRequestStatus.Accepted,
                SlotStart = WindowStart,
                SlotEnd = WindowStart.AddMinutes(30),
                CreatedAt = SimfClock.Now,
            });
            await db.SaveChangesAsync();
        }

        var slots = await GetSlotsAsync(speakerId, admin);
        Assert.Single(slots);
        Assert.Equal(WindowStart.AddMinutes(30), slots[0].Start); // only the 2nd slot remains
    }

    [Fact]
    public async Task An_awaiting_speaker_meeting_slot_is_excluded_from_the_free_slots()
    {
        // #8 regression — a hall-bound accept moves the request to AwaitingSpeaker,
        // which HOLDS the speaker's slot (MeetingRequestStatuses.SlotHolding). The
        // taken-filter must exclude it, not only Accepted rows; otherwise the slot is
        // advertised as free and a second request is created that can never be accepted.
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);

        // Mark the first slot as held by an AwaitingSpeaker (hall-bound) meeting.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
            {
                Id = Guid.NewGuid(),
                SpeakerId = speakerId,
                RequestedByUserId = Guid.NewGuid(),
                RequesterName = "VIP",
                Subject = "Slot held pending speaker confirmation",
                Status = MeetingRequestStatus.AwaitingSpeaker,
                SlotStart = WindowStart,
                SlotEnd = WindowStart.AddMinutes(30),
                CreatedAt = SimfClock.Now,
            });
            await db.SaveChangesAsync();
        }

        var slots = await GetSlotsAsync(speakerId, admin);
        Assert.Single(slots);
        Assert.Equal(WindowStart.AddMinutes(30), slots[0].Start); // only the 2nd slot remains
    }

    [Fact]
    public async Task An_invalid_window_is_400_and_an_unknown_speaker_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();

        // End before start → 400.
        var bad = await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(-10), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Unknown speaker → 404.
        var unknown = await PostAuthAsync(
            $"/api/v1/admin/speakers/{Guid.NewGuid()}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Delete_window_removes_its_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        var create = await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        var window = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerAvailabilityWindow>>())!.Data!;

        var del = await SendAuthAsync(HttpMethod.Delete,
            $"/api/v1/admin/speaker-availability-windows/{window.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        Assert.Empty(await GetWindowsAsync(speakerId, admin));
        Assert.Empty(await GetSlotsAsync(speakerId, admin));
    }

    [Fact]
    public async Task Create_window_outside_the_forum_window_is_400()
    {
        // D-753 — a window AFTER the last forum day (2026-11-22) is rejected. The date
        // is in the future so it passes every other check; only the forum-day bound
        // trips, returning the standard 400.
        var admin = await CreateAdministratorAndSignInAsync();
        var speakerId = await SeedSpeakerAsync();
        var outside = new DateTime(2026, 12, 15, 10, 0, 0);

        var resp = await PostAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/availability-windows",
            new CreateSpeakerAvailabilityWindowRequest
            {
                Start = outside, End = outside.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<AdminSpeakerAvailabilityWindow>> GetWindowsAsync(
        Guid speakerId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/speakers/{speakerId}/availability-windows", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSpeakerAvailabilityWindow>>>())!.Data!;
    }

    private async Task<IReadOnlyList<SpeakerAvailableSlot>> GetSlotsAsync(Guid speakerId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/app/speakers/{speakerId}/available-slots", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<SpeakerAvailableSlot>>>())!.Data!;
    }

    private async Task<Guid> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Test Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"avail-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Avail Test Admin",
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
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthAsync(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
