using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-124 integration tests for the scoped pending-profile read endpoints
/// (<c>GET /admin/visitors/{id}/profile-for-approval</c> and
/// <c>GET /admin/others/{id}/profile-for-approval</c>). Confirms the
/// single-404-for-all-mismatch policy that closes the enumeration hole:
/// unknown id, approved target, and wrong-type id all return the same
/// 404 + ErrorCodes.NotFound.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class PendingProfileReadTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PendingProfileReadTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_read_a_pending_visitor_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken, displayName: "Pending Visitor");

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!;
        Assert.NotNull(body.Data);
        Assert.Equal(visitor, body.Data!.Id);
        Assert.Equal("Visitor", body.Data.UserType);
        Assert.Equal("Pending Visitor", body.Data.DisplayName);
        // An admin-created visitor has no profile row yet — the response carries the
        // identity fields plus an empty interest list and HasIdImage=false.
        Assert.Empty(body.Data.InterestIds);
        Assert.False(body.Data.HasIdImage);
        // No profile fields filled → gender is the default enum string.
        Assert.Equal("Unspecified", body.Data.Gender);
    }

    [Fact]
    public async Task Admin_read_of_a_visitor_without_an_organisation_returns_null_org_fields()
    {
        // CS-C (D-385) — the Organisation left-join must return cleanly (null,
        // not throw) when the visitor picked no organisation.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken, displayName: "No Org Visitor");

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var profile = await appDb.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == visitor);
            if (profile is null)
            {
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = visitor,
                    CreatedAt = SimfClock.Now,
                };
                appDb.UserProfiles.Add(profile);
            }
            profile.Gender = Gender.Female;
            profile.OrganisationId = null;
            await appDb.SaveChangesAsync();
        }

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content
            .ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!.Data!;
        Assert.Equal("Female", data.Gender);
        Assert.Null(data.OrganisationId);
        Assert.Null(data.OrganisationName);
        Assert.Null(data.OrganisationNameArabic);
    }

    [Fact]
    public async Task Admin_read_returns_the_full_profile_data()
    {
        // CS-C (D-385) — the approval read must surface ALL captured profile
        // data: gender, organisation (bilingual), plate, reference, job title
        // and the interest NAMES (not just the count).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken, displayName: "Full Data Visitor");

        Guid orgId;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var org = new SIMF.Domain.Organisations.Organisation
            {
                Id = Guid.NewGuid(),
                Name = "Royal Saudi Naval Forces",
                NameArabic = "القوات البحرية الملكية السعودية",
                CommercialRegistration = $"CR{Guid.NewGuid():N}"[..12],
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.Organisations.Add(org);
            orgId = org.Id;

            var interestA = new UserInterest
            {
                Id = Guid.NewGuid(),
                Name = "Naval Defence",
                NameArabic = "الدفاع البحري",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            var interestB = new UserInterest
            {
                Id = Guid.NewGuid(),
                Name = "Shipbuilding",
                NameArabic = "بناء السفن",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.AddRange(interestA, interestB);

            var profile = await appDb.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == visitor);
            if (profile is null)
            {
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = visitor,
                    CreatedAt = SimfClock.Now,
                };
                appDb.UserProfiles.Add(profile);
            }
            profile.Gender = Gender.Male;
            profile.OrganisationId = orgId;
            profile.PlateNumber = "ABC1234";
            profile.ReferenceNumber = "SIMF-2026-00000042";
            profile.JobTitle = "Captain";
            profile.Interests = new List<UserInterest> { interestA, interestB };
            await appDb.SaveChangesAsync();
        }

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content
            .ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!.Data!;
        Assert.Equal("Male", data.Gender);
        Assert.Equal(orgId, data.OrganisationId);
        Assert.Equal("Royal Saudi Naval Forces", data.OrganisationName);
        Assert.Equal("القوات البحرية الملكية السعودية", data.OrganisationNameArabic);
        Assert.Equal("ABC1234", data.PlateNumber);
        Assert.Equal("SIMF-2026-00000042", data.ReferenceNumber);
        Assert.Equal("Captain", data.JobTitle);
        Assert.NotNull(data.Interests);
        Assert.Equal(2, data.Interests!.Count);
        Assert.Contains(data.Interests, i => i.Name == "Naval Defence");
        Assert.Contains(data.Interests, i => i.Name == "Shipbuilding");
    }

    [Fact]
    public async Task Admin_can_read_a_pending_other_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        var response = await GetAuthAsync(
            $"/api/v1/admin/others/{otherId}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!;
        Assert.NotNull(body.Data);
        // D-186: Other accounts are now Visitor-typed; the partner
        // status lives on the linked ProfileType.IsVisitor=false. The
        // wire shape exposes the SimfUser.UserType (now Visitor) and
        // the CP infers audience-vs-partner from the linked profile.
        Assert.Equal("Visitor", body.Data!.UserType);
    }

    [Fact]
    public async Task Full_other_profile_read_reports_HasAvatar_once_a_photo_is_set()
    {
        // D-727 (owner item 5) — the CP view / pending-review renders the staff
        // photo only when HasAvatar is true, so the full-profile read
        // (GET /admin/others/{id}/profile → AdminUserProfileView) must reflect
        // the subject's avatar (SimfUser.AvatarFileId) presence.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        // No photo yet → HasAvatar false.
        var before = await GetAuthAsync(
            $"/api/v1/admin/others/{otherId}/profile", adminToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeView = (await before.Content
            .ReadFromJsonAsync<ApiResult<AdminUserProfileView>>())!.Data!;
        Assert.False(beforeView.HasAvatar);

        // Set the avatar sentinel on the subject (SimfUser / Identity).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == otherId);
            user.AvatarFileId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        var after = await GetAuthAsync(
            $"/api/v1/admin/others/{otherId}/profile", adminToken);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterView = (await after.Content
            .ReadFromJsonAsync<ApiResult<AdminUserProfileView>>())!.Data!;
        Assert.True(afterView.HasAvatar);
        Assert.False(afterView.HasIdImage); // control: the ID image is untouched
    }

    [Fact]
    public async Task Others_pending_list_row_reports_HasAvatar_once_a_photo_is_set()
    {
        // Phase B (D-568 parity) — the pending queue grid renders the applicant's
        // photo thumbnail, so the pending-list row (AdminPendingUserSummary) must
        // carry HasAvatar from the SimfUser.AvatarFileId sentinel, not only
        // the single-profile read.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        // No photo yet → the pending-list row's HasAvatar is false.
        Assert.False((await FindPendingOtherRowAsync(adminToken, otherId)).HasAvatar);

        // Set the avatar sentinel on the subject (SimfUser / Identity).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == otherId);
            user.AvatarFileId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }

        // The list projection now reports the photo.
        Assert.True((await FindPendingOtherRowAsync(adminToken, otherId)).HasAvatar);
    }

    // Fetches one page of the pending-Others queue (newest-first, so a
    // just-created row is on the first page) and returns the row for the subject.
    private async Task<AdminPendingUserSummary> FindPendingOtherRowAsync(
        string adminToken, Guid otherId)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/others/pending/list",
            new GridQuery { Top = 100 }, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!.Data!;
        var row = page.Items.SingleOrDefault(r => r.Id == otherId);
        Assert.NotNull(row);
        return row!;
    }

    [Fact]
    public async Task Reading_an_approved_visitor_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        // Approve so the AccountState moves out of PendingApproval — the
        // read endpoint must then deny because it is scoped to Pending.
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.NotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Reading_a_pending_visitor_via_others_endpoint_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        // Wrong type — the /others/.../profile-for-approval endpoint must
        // refuse a Visitor id with the same 404 a missing id would emit.
        var response = await GetAuthAsync(
            $"/api/v1/admin/others/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_pending_other_via_visitors_endpoint_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{otherId}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_an_unknown_id_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_non_admin_caller_is_forbidden()
    {
        var visitorTokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}/profile-for-approval",
            visitorTokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_read_does_not_mutate_account_state()
    {
        // Spec C item (g) intended a row-audit assertion on read — but
        // D-109's SaveChanges interceptor only fires on writes, so a
        // pure read never produces a RowAudit row. This replacement
        // case proves the read is non-destructive (AccountState stays
        // PendingApproval after the read), which is the behaviour the
        // approve / reject flow depends on.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        var read = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.Users.SingleAsync(u => u.Id == visitor);
        Assert.Equal(AccountState.PendingApproval, row.AccountState);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreatePendingVisitorAsync(
        string adminToken, string? displayName = null)
    {
        var email = $"pending-v-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = displayName ?? "Pending Visitor Subject",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> CreatePendingOtherAsync(string adminToken)
    {
        var profileTypeId = await GetSeededOtherProfileTypeAsync();
        var email = $"pending-o-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = "Pending Other Subject",
                ProfileTypeId = profileTypeId,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> GetSeededOtherProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // D-186: partner-side profile types live under UserType.Visitor
        // with IsVisitor=false. The CP "Others" queue filters this set.
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor == false
                                       && p.IsActive);
        if (seeded is not null) { return seeded.Id; }
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — PendingReadTestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            IsForVisitor = false,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(fresh);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"pending-read-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Pending Read Tests Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email, Password);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return _client.SendAsync(request);
    }
}
