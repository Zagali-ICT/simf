// Region lookup module — admin CRUD + the public app picker read.
// Mirrors OrganisationTests (admin CRUD round-trip, duplicate-key 409, auth-gate)
// minus the Excel import (Region has no bulk import); adds the public
// GET /app/regions read over the 13 seeded Saudi regions.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Regions;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for the Region lookup module: admin CRUD
/// (create → list → get → update → deactivate), duplicate-Code conflict,
/// validation, the admin auth-gate, and the signed-in public picker read.</summary>
public sealed class RegionTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RegionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_update_then_deactivate_round_trip()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();

        // Create.
        var create = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest
            {
                Code = code,
                NameArabic = "منطقة الاختبار",
                Name = "Test Region",
                SortOrder = 50,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminRegionDetail>>())!.Data!;
        Assert.Equal(code, created.Code);
        Assert.Equal("منطقة الاختبار", created.NameArabic);
        Assert.Equal("Test Region", created.Name);
        Assert.Equal(50, created.SortOrder);
        Assert.True(created.IsActive);

        // List contains the new region.
        var list = await PostAuthAsync(
            "/api/v1/admin/regions/list",
            new GridQuery { Top = 200 },
            token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminRegionSummary>>>())!.Data!;
        Assert.Contains(page.Items, r => r.Id == created.Id);

        // Get by id.
        var get = await GetAuthAsync($"/api/v1/admin/regions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminRegionDetail>>())!.Data!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(code, fetched.Code);

        // Update — change the English name and sort order.
        var update = await PutAuthAsync(
            $"/api/v1/admin/regions/{created.Id}",
            new UpdateRegionRequest
            {
                Code = code,
                NameArabic = "منطقة الاختبار",
                Name = "Test Region Renamed",
                SortOrder = 7,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminRegionDetail>>())!.Data!;
        Assert.Equal("Test Region Renamed", updated.Name);
        Assert.Equal(7, updated.SortOrder);

        // Deactivate (soft-delete).
        var delete = await DeleteAuthAsync($"/api/v1/admin/regions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var afterDelete = await GetAuthAsync($"/api/v1/admin/regions/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, afterDelete.StatusCode);
        var deactivated = (await afterDelete.Content
            .ReadFromJsonAsync<ApiResult<AdminRegionDetail>>())!.Data!;
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_REGION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var first = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest { Code = code, NameArabic = "منطقة أولى", SortOrder = 1 },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest { Code = code, NameArabic = "منطقة ثانية", SortOrder = 2 },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RegionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Missing_arabic_name_is_a_400_REGION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest { Code = NewCode(), NameArabic = "   ", SortOrder = 1 },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RegionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Over_length_arabic_name_is_a_400_REGION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest
            {
                Code = NewCode(),
                NameArabic = new string('م', 257), // > 256
                SortOrder = 1,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RegionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/regions/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest { Code = NewCode(), NameArabic = "منطقة الزائر", SortOrder = 1 },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Public_picker_returns_the_seeded_active_regions_ordered_by_sort_order()
    {
        // The picker read is authenticated (signed-in user completing their
        // profile); it is deliberately NOT anonymous, so pass the bearer token.
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/app/regions", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<RegionPickerItem>>>())!.Data!;

        // The RegionSeeder inserts the 13 official Saudi regions; Riyadh is
        // SortOrder 0, so it is first in the picker ordering.
        Assert.Contains(items, item => item.Code == "riyadh");
        Assert.Contains(items, item => item.Code == "makkah");
        Assert.Equal("riyadh", items[0].Code);
    }

    [Fact]
    public async Task Public_picker_excludes_a_deactivated_region()
    {
        // Seed a region, then deactivate it; it must not surface in the picker.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/regions",
            new CreateRegionRequest { Code = code, NameArabic = "منطقة مخفية", SortOrder = 99 },
            adminToken);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminRegionDetail>>())!.Data!;
        var delete = await DeleteAuthAsync($"/api/v1/admin/regions/{created.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/app/regions", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<RegionPickerItem>>>())!.Data!;
        Assert.DoesNotContain(items, item => item.Code == code);
    }

    [Fact]
    public async Task Public_picker_requires_sign_in()
    {
        // No bearer token — the endpoint is not AllowAnonymous, so the caller is
        // rejected as unauthenticated.
        var response = await _client.GetAsync("/api/v1/app/regions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => Guid.NewGuid().ToString("N")[..10].ToLowerInvariant();

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"region-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Region Admin",
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
