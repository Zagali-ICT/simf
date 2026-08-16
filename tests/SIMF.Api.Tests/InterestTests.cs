using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the Interests admin CRUD + the visitor-facing
/// active list (P9 — D-050; الاهتمامات).
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class InterestTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public InterestTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_create_list_and_get_an_interest()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Maritime Security {Guid.NewGuid():N}";

        var create = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = name,
                NameArabic = "الأمن البحري",
                DisplayOrder = 1,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var summary = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminInterestSummary>>())!.Data!;
        Assert.Equal(name, summary.Name);
        Assert.True(summary.IsActive);

        var list = await PostAuthAsync(
            "/api/v1/admin/interests/list",
            new GridQuery { Top = 50 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminInterestSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Id == summary.Id);

        var get = await GetAuthAsync($"/api/v1/admin/interests/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_create_a_duplicate_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Logistics {Guid.NewGuid():N}";

        var first = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = name,
                NameArabic = "اللوجستيات",
                DisplayOrder = 0,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = name,
                NameArabic = "اللوجستيات",
                DisplayOrder = 0,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestNameDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_can_update_and_deactivate_an_interest()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = $"Robotics {Guid.NewGuid():N}",
                NameArabic = "الروبوتات",
                DisplayOrder = 5,
            },
            adminToken);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminInterestSummary>>())!.Data!;

        var newName = $"Robotics & Autonomy {Guid.NewGuid():N}";
        var update = await PutAuthAsync(
            $"/api/v1/admin/interests/{created.Id}",
            new AdminUpdateInterestRequest
            {
                Name = newName,
                NameArabic = "الروبوتات والقيادة الذاتية",
                DisplayOrder = 2,
                IsActive = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminInterestSummary>>())!.Data!;
        Assert.Equal(newName, updated.Name);
        Assert.Equal(2, updated.DisplayOrder);

        var deactivate = await DeleteAuthAsync(
            $"/api/v1/admin/interests/{created.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // The visitor list filters by IsActive — confirm.
        var visitorList = await GetAuthAsync("/api/v1/app/account/interests", adminToken);
        var visitorBody = (await visitorList.Content
            .ReadFromJsonAsync<ApiResult<InterestListResponse>>())!.Data!;
        Assert.DoesNotContain(visitorBody.Interests, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Visitor_list_returns_only_active_interests_ordered()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        // Seed two — one active, one inactive.
        await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = $"Active {Guid.NewGuid():N}",
                NameArabic = "مفعّل",
                DisplayOrder = 10,
            },
            adminToken);

        var dormant = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = $"Dormant {Guid.NewGuid():N}",
                NameArabic = "غير مفعّل",
                DisplayOrder = 11,
            },
            adminToken);
        var dormantId = (await dormant.Content
            .ReadFromJsonAsync<ApiResult<AdminInterestSummary>>())!.Data!.Id;
        await DeleteAuthAsync($"/api/v1/admin/interests/{dormantId}", adminToken);

        var visitorList = await GetAuthAsync("/api/v1/app/account/interests", adminToken);
        var body = (await visitorList.Content
            .ReadFromJsonAsync<ApiResult<InterestListResponse>>())!.Data!;
        Assert.DoesNotContain(body.Interests, item => item.Id == dormantId);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_the_create_endpoint()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/interests",
            new AdminCreateInterestRequest
            {
                Name = "Should Not Land",
                NameArabic = "لن يُسجَّل",
                DisplayOrder = 0,
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_interest_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/interests/{Guid.NewGuid()}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"interest-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Interest Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
