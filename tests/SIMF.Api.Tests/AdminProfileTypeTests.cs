using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-115 — integration tests for the admin ProfileType CRUD surface
/// (<c>/admin/profile-types/*</c>). Confirms per-UserType name
/// uniqueness, cross-UserType same-name allowed, immutable UserType
/// post-create, soft-delete with in-use protection, and the 403 floor
/// for non-admin callers.
/// </summary>
public sealed class AdminProfileTypeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminProfileTypeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_create_get_list_and_soft_delete_a_visitor_profile_type()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"VVIP {Guid.NewGuid():N}";

        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                Name = name,
                NameArabic = "كبار الشخصيات",
                PageColor = "#FFD700",
                IsActive = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var summary = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(name, summary.Name);
        Assert.Equal("Visitor", summary.UserType);
        Assert.True(summary.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery { Top = 100 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Id == summary.Id);

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        // Soft-delete is reflected in a subsequent get.
        var afterDelete = await GetAuthAsync($"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        var afterDeleteBody = (await afterDelete.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(afterDeleteBody.IsActive);
    }

    [Fact]
    public async Task Admin_can_update_a_profile_type_without_touching_user_type()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Sponsor {Guid.NewGuid():N}";

        // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
        var created = await CreateProfileTypeAsync(adminToken, "Visitor", name, "راعٍ", "#3B82F6");

        var renamed = $"{name} (Platinum)";
        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{created.Id}",
            new
            {
                Name = renamed,
                NameArabic = "راعٍ بلاتيني",
                PageColor = "#FFFFFF",
                IsActive = true,
                IsVisitor = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(renamed, updated.Name);
        // The route doesn't accept UserType in the body, so the value
        // cannot drift after create. D-186: every non-admin row is Visitor.
        Assert.Equal("Visitor", updated.UserType);
    }

    [Fact]
    public async Task Duplicate_name_within_the_same_user_type_returns_409()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Exhibitor {Guid.NewGuid():N}";

        // D-186: partner-side profile types live under UserType.Visitor.
        await CreateProfileTypeAsync(adminToken, "Visitor", name, "عارض", "#10B981");
        var second = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "عارض",
                PageColor = "#10B981",
                IsActive = true,
            },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeNameTaken, body.Error!.Code);
    }

    [Fact]
    public async Task Same_name_across_audience_and_partner_scope_returns_409()
    {
        // D-186: Visitor + Other used to be two distinct UserType scopes,
        // each with its own name-uniqueness bucket. After D-186 both are
        // UserType.Visitor; the audience-vs-partner split is IsVisitor.
        // The unique constraint is per (UserType, Name) so the same name
        // can no longer coexist across audience + partner. Documented as
        // expected behaviour — operators rename one of the rows.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Gold {Guid.NewGuid():N}";

        var audienceRow = await CreateProfileTypeAsync(
            adminToken, "Visitor", name, "ذهبي", "#FFD700");

        var partnerAttempt = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "ذهبي",
                PageColor = "#FFD700",
                IsActive = true,
            },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, partnerAttempt.StatusCode);
        Assert.Equal("Visitor", audienceRow.UserType);
        Assert.True(audienceRow.IsVisitor);
    }

    [Fact]
    public async Task Create_for_Admin_user_type_returns_400()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Admin",
                Name = $"Should not land {Guid.NewGuid():N}",
                NameArabic = "لن يُسجَّل",
                PageColor = "#000000",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeInvalidUserType, body.Error!.Code);
    }

    [Fact]
    public async Task Cannot_delete_a_profile_type_that_is_still_referenced_by_a_user_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        // Create a profile type and then attach a UserProfile to it.
        var pt = await CreateProfileTypeAsync(adminToken, "Visitor",
            $"InUse {Guid.NewGuid():N}", "قيد الاستخدام", "#3B82F6");

        Guid visitorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var v = new SimfUser
            {
                UserName = $"ptuser-{Guid.NewGuid():N}@simf.test",
                Email = $"ptuser-{Guid.NewGuid():N}@simf.test",
                EmailConfirmed = true,
                DisplayName = "Has Profile Type",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(v, AuthFlow.Password);
            visitorId = v.Id;

            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                ProfileTypeId = pt.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
        }

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{pt.Id}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeInUse, body.Error!.Code);
    }

    [Fact]
    public async Task A_non_admin_caller_is_forbidden_from_every_profile_type_endpoint()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery { Top = 10 },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                Name = "Should not land",
                NameArabic = "لن يُسجَّل",
                PageColor = "#000000",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{Guid.NewGuid()}", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{Guid.NewGuid()}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<AdminProfileTypeSummary> CreateProfileTypeAsync(
        string adminToken, string userType, string name, string nameArabic, string pageColor)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = userType,
                Name = name,
                NameArabic = nameArabic,
                PageColor = pageColor,
                IsActive = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"pt-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "ProfileType Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
