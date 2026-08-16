// Tests: D-500 (Wave 5, الطلبات "طلب تحديث البادج") — badge job-title update
// request submission + admin review, and the apply-to-profile on Accept
// (BadgeUpdateRequestService + BadgeUpdateRequestEndpoints).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Requests;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Badges)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class BadgeUpdateRequestsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeUpdateRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submission_requires_authentication()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/badge-requests", new { requestedJobTitle = "Captain" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_requires_a_job_title()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        var response = await SendAuthAsync(HttpMethod.Post, "/api/v1/app/badge-requests",
            token, new { requestedJobTitle = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_creates_a_pending_request()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        var submitted = await SubmitAsync(token, "Naval Captain");
        Assert.Equal(MeetingRequestStatus.Pending, submitted.Status);
    }

    [Fact]
    public async Task Accepting_applies_the_requested_title_to_the_profile()
    {
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedProfileAsync(userId, jobTitle: "Lieutenant");
        var submitted = await SubmitAsync(token, "Commander");
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/badge-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Accepted });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var title = await db.UserProfiles.Where(p => p.UserId == userId)
            .Select(p => p.JobTitle).SingleAsync();
        Assert.Equal("Commander", title);
    }

    [Fact]
    public async Task Admin_list_requires_permission()
    {
        var (token, _) = await SignInApprovedVisitorAsync();
        var response = await SendAuthAsync(HttpMethod.Post,
            "/api/v1/admin/badge-requests/list", token, new GridQuery());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Re_deciding_a_responded_request_is_409_and_leaves_the_profile_untouched()
    {
        // A1 — an Accept after the request was already decided is rejected, so the
        // JobTitle side effect cannot replay on a non-Pending request.
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedProfileAsync(userId, jobTitle: "Lieutenant");
        var submitted = await SubmitAsync(token, "Commander");
        var admin = await CreateAdministratorAndSignInAsync();

        var reject = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/badge-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Rejected });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var accept = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/badge-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Accepted });
        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
        var body = (await accept.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestAlreadyResponded, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var title = await db.UserProfiles.Where(p => p.UserId == userId)
            .Select(p => p.JobTitle).SingleAsync();
        Assert.Equal("Lieutenant", title);
    }

    [Fact]
    public async Task Accepting_a_badge_update_dispatches_a_BadgeUpdateDecided_notification_and_still_applies_the_JobTitle()
    {
        // R-2 — the accept still applies the title AND now tells the requester.
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedProfileAsync(userId, jobTitle: "Lieutenant");
        var submitted = await SubmitAsync(token, "Commander");
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/badge-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Accepted });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var title = await db.UserProfiles.Where(p => p.UserId == userId)
            .Select(p => p.JobTitle).SingleAsync();
        Assert.Equal("Commander", title);

        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.BadgeUpdateDecided
            && n.RelatedEntityId == submitted.Id
            && n.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Rejecting_a_badge_update_dispatches_the_notification_and_does_not_touch_the_JobTitle()
    {
        // R-2 — a reject notifies but leaves the profile title alone.
        var (token, userId) = await SignInApprovedVisitorAsync();
        await SeedProfileAsync(userId, jobTitle: "Lieutenant");
        var submitted = await SubmitAsync(token, "Commander");
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/badge-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Rejected, responseNote = "No change." });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var title = await db.UserProfiles.Where(p => p.UserId == userId)
            .Select(p => p.JobTitle).SingleAsync();
        Assert.Equal("Lieutenant", title);

        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.BadgeUpdateDecided
            && n.RelatedEntityId == submitted.Id
            && n.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Submitting_a_second_pending_badge_update_is_409()
    {
        // R-4 — one open badge-update request per requester.
        var (token, _) = await SignInApprovedVisitorAsync();
        await SubmitAsync(token, "Commander");

        var second = await SendAuthAsync(HttpMethod.Post, "/api/v1/app/badge-requests",
            token, new { requestedJobTitle = "Captain" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AppRequestDuplicatePending, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<BadgeUpdateRequestSubmitted> SubmitAsync(string token, string title)
    {
        var response = await SendAuthAsync(HttpMethod.Post, "/api/v1/app/badge-requests",
            token, new { requestedJobTitle = title });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<BadgeUpdateRequestSubmitted>>())!.Data!;
    }

    private async Task SeedProfileAsync(Guid userId, string jobTitle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Badge Visitor",
            NameArabic = "زائر",
            JobTitle = jobTitle,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(string Token, Guid UserId)> SignInApprovedVisitorAsync()
    {
        var email = $"badge-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!
            .Data!.Tokens!.AccessToken;

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            userId = await idDb.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        }
        return (token, userId);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"badge-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Badge Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method, string url, string token, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
