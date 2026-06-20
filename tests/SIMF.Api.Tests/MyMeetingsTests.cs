// Tests: D-479 (#11 follow-up) — read-only "My meetings" feed for the mobile app.
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
using SIMF.Domain.Contacts;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-479 (#11 follow-up) — GET /app/my-meetings returns the signed-in user's own
/// speaker (D-269) + delegation (D-478) meeting requests, newest first, and never
/// another user's. Read-only; delegation creation/management stays on the CP.
/// </summary>
public sealed class MyMeetingsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MyMeetingsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task My_meetings_returns_my_speaker_and_delegation_requests_newest_first()
    {
        var (token, userId) = await CreateVisitorAsync();
        var speakerId = await SeedSpeakerAsync("Captain Reef");
        var (homeId, targetId) = await SeedInvitedCountriesAsync();

        // A speaker request (older) + a delegation request (newer), both mine.
        await SeedSpeakerMeetingRequestAsync(speakerId, userId,
            "Trade talk", new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero));
        await SeedDelegationMeetingRequestAsync(homeId, targetId, userId,
            "Naval cooperation", new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.Zero));

        var response = await GetAuthAsync("/api/v1/app/my-meetings", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<List<MyMeetingItem>>>())!;
        Assert.Equal(2, body.Data!.Count);
        // Newest first: the delegation request was created a day later.
        Assert.Equal(MyMeetingKind.DelegationMeeting, body.Data[0].Kind);
        Assert.Equal(MyMeetingKind.SpeakerMeeting, body.Data[1].Kind);
        Assert.Equal("Captain Reef", body.Data[1].Title);
        Assert.Contains(body.Data, m => m.Subject == "Naval cooperation");
    }

    [Fact]
    public async Task My_meetings_does_not_return_another_users_meetings()
    {
        var (_, ownerId) = await CreateVisitorAsync();
        var (otherToken, _) = await CreateVisitorAsync();
        var speakerId = await SeedSpeakerAsync("Captain Tide");
        await SeedSpeakerMeetingRequestAsync(speakerId, ownerId,
            "Owner-only", new DateTimeOffset(2030, 3, 1, 9, 0, 0, TimeSpan.Zero));

        var response = await GetAuthAsync("/api/v1/app/my-meetings", otherToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<List<MyMeetingItem>>>())!;
        Assert.DoesNotContain(body.Data!, m => m.Subject == "Owner-only");
    }

    // -- helpers --------------------------------------------------------------

    private async Task<(string Token, Guid UserId)> CreateVisitorAsync()
    {
        var email = $"mym-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "My Meetings Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var type = await appDb.ProfileTypes.FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
            if (type is null)
            {
                type = new UserProfileType
                {
                    Id = Guid.NewGuid(),
                    Name = "Visitor — MyMeetingsSeed", NameArabic = "زائر",
                    PageColor = "#3B82F6", IsForVisitor = true, IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                appDb.ProfileTypes.Add(type);
                await appDb.SaveChangesAsync();
            }
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = type.Id,
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

    private async Task<Guid> SeedSpeakerAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var contact = new Contact
        {
            Id = Guid.NewGuid(), NameArabic = "متحدّث",
            Email = $"spk-{Guid.NewGuid():N}@simf.test",
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Contacts.Add(contact);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = name, NameArabic = "متحدّث",
            AllowsMeetingRequests = true,
            ContactId = contact.Id,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task<(int HomeId, int TargetId)> SeedInvitedCountriesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var home = await EnsureCountryAsync(db, "SA", 682);
        var target = await EnsureCountryAsync(db, "EG", 818);
        await db.SaveChangesAsync();
        return (home, target);

        static async Task<int> EnsureCountryAsync(SimfAppDbContext db, string code, int id)
        {
            var c = await db.Countries.FirstOrDefaultAsync(x => x.Code == code);
            if (c is null)
            {
                c = new Country
                {
                    Id = id, Code = code, Name = code, NameArabic = code,
                    IsActive = true, IsInvited = true, CreatedAt = DateTimeOffset.UtcNow,
                };
                db.Countries.Add(c);
            }
            return c.Id;
        }
    }

    private async Task SeedSpeakerMeetingRequestAsync(
        Guid speakerId, Guid userId, string subject, DateTimeOffset createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            RequestedByUserId = userId,
            RequesterName = "My Meetings Visitor",
            Subject = subject,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedDelegationMeetingRequestAsync(
        int homeId, int targetId, Guid userId, string subject, DateTimeOffset createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = userId,
            RequestingCountryId = homeId,
            TargetCountryId = targetId,
            AttendeeCount = 5,
            Subject = subject,
            Status = MeetingRequestStatus.Pending,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
