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
using SIMF.Domain.Contacts;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
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
        Assert.Equal(WindowStart, req.SlotStartUtc);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
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

    // -- helpers --------------------------------------------------------------

    private static SubmitSpeakerMeetingRequestRequest SlotRequest(
        DateTimeOffset start, DateTimeOffset end) =>
        new()
        {
            RequesterName = "VIP Guest",
            Subject = "Partnership discussion",
            SlotStartUtc = start,
            SlotEndUtc = end,
        };

    private async Task<Guid> SeedSpeakerWithWindowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            NameArabic = "متحدّث",
            Email = "speaker@simf.test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Contacts.Add(contact);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Test Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            ContactId = contact.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            StartUtc = WindowStart,
            EndUtc = WindowStart.AddMinutes(60),
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
