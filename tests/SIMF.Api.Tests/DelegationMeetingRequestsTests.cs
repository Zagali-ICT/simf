// Tests: D-478 (#11, Group G phase 2) — delegation↔delegation (G2G) meeting requests.
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
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-478 (#11, Group G phase 2) — a delegate (وفد) of an invited country requests
/// their delegation meets another invited country's delegation ("count X meets
/// country Y"); the team Accepts/Rejects and the requester is notified + emailed.
/// </summary>
public sealed class DelegationMeetingRequestsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationMeetingRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_delegate_submits_and_the_request_is_pending_with_the_target()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        var targetId = await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG",
                AttendeeCount = 10,
                Subject = "Naval cooperation talks",
            },
            delegate1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.TargetCountryId == targetId);
        Assert.Equal(homeId, req.RequestingCountryId);
        Assert.Equal(10, req.AttendeeCount);
        Assert.Equal(MeetingRequestStatus.Pending, req.Status);
    }

    [Fact]
    public async Task A_non_delegate_submit_is_403()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (plain, _) = await CreateDelegateAsync(homeId, isDelegate: false);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "Hello",
            },
            plain);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_target_country_that_is_not_invited_is_400()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("US", 840, invited: false);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "US", AttendeeCount = 5, Subject = "Hello",
            },
            delegate1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!;
        Assert.Equal(ErrorCodes.DelegateCountryNotInvited, body.Error!.Code);
    }

    [Fact]
    public async Task Accepting_a_request_notifies_the_requester()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, delegateUserId) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 8, Subject = "Cooperation",
            },
            delegate1);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        var respond = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var notified = await identity.Notifications.AnyAsync(n => n.UserId == delegateUserId);
        Assert.True(notified);
    }

    [Fact]
    public async Task Admin_can_list_the_delegation_meeting_requests()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 6, Subject = "Listing probe",
            },
            delegate1);

        var list = await PostAuthAsync(
            "/api/v1/admin/delegation-meeting-requests/list",
            new GridQuery { Skip = 0, Top = 50 },
            admin);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminDelegationMeetingRequestRow>>>())!;
        Assert.Contains(body.Data!.Items, r => r.Subject == "Listing probe");
    }

    [Fact]
    public async Task A_second_pending_request_for_the_same_target_is_rejected()
    {
        // A1 — one open request per (requester, target delegation).
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);

        var first = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "First",
            },
            delegate1);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "Second",
            },
            delegate1);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestDuplicatePending, body.Error!.Code);
    }

    [Fact]
    public async Task Responding_to_an_already_decided_request_is_409()
    {
        // A1 — only a Pending request may be decided.
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "Topic",
            },
            delegate1);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        var first = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Rejected },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestAlreadyResponded, body.Error!.Code);
    }

    [Fact]
    public async Task A_slot_start_without_an_end_is_400()
    {
        // A1 — a half-specified slot pair is rejected rather than stored silently.
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);

        var response = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "Topic",
                SlotStartUtc = new DateTimeOffset(2030, 2, 1, 9, 0, 0, TimeSpan.Zero),
            },
            delegate1);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Responding_with_Cancelled_status_is_400()
    {
        // A1 (review) — only Accepted/Rejected are valid admin responses.
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var submit = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = "EG", AttendeeCount = 5, Subject = "Topic",
            },
            delegate1);
        var requestId = (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;

        var respond = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Cancelled },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);
    }

    // -- M-3: accept validates the proposed slot (not-in-past + no clash) ------

    [Fact]
    public async Task Accepting_a_request_with_a_free_future_slot_succeeds()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        var slotStart = new DateTimeOffset(2030, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var requestId = await SubmitWithSlotAsync(delegate1, "EG", slotStart, slotStart.AddHours(1));

        var respond = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Accepted, req.Status);
        Assert.Equal(slotStart, req.SlotStartUtc);
    }

    [Fact]
    public async Task Accepting_a_request_with_a_slot_in_the_past_is_400()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        // Submit only checks end>start, so a past-but-well-formed slot persists.
        var slotStart = new DateTimeOffset(2020, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var requestId = await SubmitWithSlotAsync(delegate1, "EG", slotStart, slotStart.AddHours(1));

        var respond = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{requestId}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Accepting_an_overlapping_slot_for_the_same_delegation_is_409()
    {
        var homeId = await EnsureCountryAsync("SA", 682, invited: true);
        await EnsureCountryAsync("EG", 818, invited: true);
        await EnsureCountryAsync("US", 840, invited: true);
        var (delegate1, _) = await CreateDelegateAsync(homeId, isDelegate: true);
        var admin = await CreateAdministratorAndSignInAsync();

        // A: SA -> EG, 09:00-10:00 — accepted. A distinct date from the other
        // slot-bearing M-3 test so the class-shared DB does not cross-contaminate.
        var slotStart = new DateTimeOffset(2035, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var reqA = await SubmitWithSlotAsync(delegate1, "EG", slotStart, slotStart.AddHours(1));
        var acceptA = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{reqA}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, acceptA.StatusCode);

        // B: SA -> US, 09:30-10:30 — shares the SA delegation, overlaps A.
        var reqB = await SubmitWithSlotAsync(
            delegate1, "US", slotStart.AddMinutes(30), slotStart.AddMinutes(90));
        var acceptB = await PostAuthAsync(
            $"/api/v1/admin/delegation-meeting-requests/{reqB}/respond",
            new RespondToDelegationMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.Conflict, acceptB.StatusCode);
        var body = (await acceptB.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegationMeetingRequestInvalid, body.Error!.Code);
    }

    private async Task<Guid> SubmitWithSlotAsync(
        string delegateToken, string targetCode, DateTimeOffset slotStart, DateTimeOffset slotEnd)
    {
        var submit = await PostAuthAsync(
            "/api/v1/app/delegation-meeting-requests",
            new SubmitDelegationMeetingRequestRequest
            {
                TargetCountryCode = targetCode,
                AttendeeCount = 5,
                Subject = "Slotted meeting",
                SlotStartUtc = slotStart,
                SlotEndUtc = slotEnd,
            },
            delegateToken);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return (await submit.Content
            .ReadFromJsonAsync<ApiResult<DelegationMeetingRequestSubmitted>>())!.Data!.Id;
    }

    // -- helpers --------------------------------------------------------------

    private async Task<int> EnsureCountryAsync(string code, int id, bool invited)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id,
                Code = code, Name = code, NameArabic = code,
                IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.Countries.Add(country);
        }
        country.IsActive = true;
        country.IsInvited = invited;
        await appDb.SaveChangesAsync();
        return country.Id;
    }

    private async Task<(string Token, Guid UserId)> CreateDelegateAsync(int nationalityId, bool isDelegate)
    {
        var email = $"dmr-{(isDelegate ? "del" : "plain")}-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Delegate One",
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
                    Name = "Visitor — DmrSeed", NameArabic = "زائر",
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
                NationalityId = nationalityId,
                IsDelegate = isDelegate,
                // Bi-Meeting rework — the delegation-meeting gate now reads this
                // per-user flag (was IsDelegate). Map the fixture's intent onto it.
                AllowsDelegationMeeting = isDelegate,
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
        var email = $"dmr-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "DMR Admin",
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
