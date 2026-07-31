// QA A24 — `MeetingLinks:PublicWebBaseUrl` ships empty, so the speaker confirmation
// email used to be skipped with only a LogWarning while the tokens were still minted
// and the row parked in AwaitingSpeaker: a state whose only exit is an email that was
// never sent. Missing link configuration is now a hard failure on that path.
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
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>QA A24 — a factory that boots the API with
/// <c>MeetingLinks:PublicWebBaseUrl</c> UNSET, reproducing the shipped
/// <c>appsettings.json</c> default. The process-wide variable is restored by the next
/// <see cref="SimfApiFactory"/> construction (test parallelism is disabled).</summary>
public sealed class MeetingLinksUnsetApiFactory : SimfApiFactory
{
    public MeetingLinksUnsetApiFactory() =>
        Environment.SetEnvironmentVariable("MeetingLinks__PublicWebBaseUrl", string.Empty);
}

public sealed class SpeakerMeetingLinksUnsetTests
    : IClassFixture<MeetingLinksUnsetApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly MeetingLinksUnsetApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerMeetingLinksUnsetTests(MeetingLinksUnsetApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A24_Approve_with_no_public_web_base_url_is_refused_and_mints_no_tokens()
    {
        var speaker = await SeedSpeakerAsync();
        var visitor = await SignInEligibleVisitorAsync();
        var created = await SubmitAsync(speaker.Id, visitor);
        var admin = await CreateAdministratorAndSignInAsync();

        var hallId = await SeedMeetingHallAsync();
        var slot = (await CreateHallWindowAndGetSlotsAsync(hallId, admin))[0];

        var respond = await PutAuthAsync(
            $"/api/v1/admin/speaker-meeting-requests/{created.Id}/respond",
            new RespondToSpeakerMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                HallId = hallId, SlotStart = slot.Start, SlotEnd = slot.End,
            }, admin);

        Assert.Equal(HttpStatusCode.Conflict, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingLinksNotConfigured, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.MeetingActionTokens
            .AnyAsync(t => t.SpeakerMeetingRequestId == created.Id));
        var row = await db.SpeakerMeetingRequests.SingleAsync(r => r.Id == created.Id);
        Assert.Equal(MeetingRequestStatus.Pending, row.Status);
    }

    // -- Helpers ----------------------------------------------------------------

    private async Task<Speaker> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. Links Unset", NameArabic = "متحدّث",
            // A contact email IS on file, so the only reason the approve can fail is
            // the missing link configuration (the A25 guard is not what fires).
            Email = "links-unset-speaker@simf.test",
            AllowsMeetingRequests = true, IsActive = true, DisplayOrder = 0,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        // G3 (owner 2026-07-30) — a submit now REQUIRES the speaker to have a free
        // slot, so this fixture speaker gets one wide future window. The
        // no-availability refusal has its own coverage in MeetingNoAvailabilityTests.
        db.SpeakerAvailabilityWindows.Add(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            Start = new DateTime(2035, 9, 1, 9, 0, 0),
            End = new DateTime(2035, 9, 1, 13, 0, 0),
            SlotMinutes = 30,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return speaker;
    }

    private async Task<Guid> SeedMeetingHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall", NameArabic = "قاعة الاجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<IReadOnlyList<HallAvailableSlot>> CreateHallWindowAndGetSlotsAsync(
        Guid hallId, string admin)
    {
        var start = new DateTime(2033, 4, 1, 9, 0, 0);
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var slots = await GetAuthAsync($"/api/v1/admin/halls/{hallId}/available-slots", admin);
        var list = (await slots.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<HallAvailableSlot>>>())!.Data!;
        Assert.NotEmpty(list);
        return list;
    }

    private async Task<SpeakerMeetingRequestSubmitted> SubmitAsync(Guid speakerId, string token)
    {
        var submit = await PostAuthAsync(
            $"/api/v1/app/speakers/{speakerId}/meeting-requests",
            new SubmitSpeakerMeetingRequestRequest
            {
                RequesterName = "QA Requester",
                Subject = "Maritime cooperation",
            }, token);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<SpeakerMeetingRequestSubmitted>>())!.Data!;
    }

    private async Task<string> SignInEligibleVisitorAsync()
    {
        var email = $"smlu-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(
                    _factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        await GrantSpeakerMeetingAccessAsync(email);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task GrantSpeakerMeetingAccessAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} was not found.");
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profileType = await appDb.ProfileTypes.FirstOrDefaultAsync(p => p.IsForVisitor);
        if (profileType is null)
        {
            profileType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "QA Visitor", NameArabic = "زائر", PageColor = "#FFD700",
                IsForVisitor = true, IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.ProfileTypes.Add(profileType);
        }
        var profile = await appDb.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is null)
        {
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = profileType.Id,
                AllowsSpeakerMeeting = true,
                Name = "QA Requester", NameArabic = "مقدّم الطلب",
                CreatedAt = SimfClock.Now,
            });
        }
        else
        {
            profile.ProfileTypeId = profileType.Id;
            profile.AllowsSpeakerMeeting = true;
        }
        await appDb.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"smlu-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "SMLU Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
