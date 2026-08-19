// P2.4 — D-229 (FDS-012 §5.5): System Configuration settings CRUD. Mirrors
// SessionCategoriesTests. The store ships empty; the team seeds keys (OI-2).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SystemSettingsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SystemSettingsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_list_contains_the_setting()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "event.contactEmail." + Guid.NewGuid().ToString("N")[..6];
        var create = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest
            {
                Key = key, Value = "info@simf.test", Description = "Public contact email",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        Assert.Equal(key, created.Key);
        Assert.Equal("info@simf.test", created.Value);

        var get = await GetAuthAsync($"/api/v1/admin/system-settings/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/system-settings/list", new GridQuery { Top = 200 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSystemSettingSummary>>>())!.Data!;
        Assert.Contains(page.Items, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/system-settings/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_empty_key_is_400_SYSTEM_SETTING_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = "   ", Value = "x" },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SystemSettingInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_active_key_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "feature.flag." + Guid.NewGuid().ToString("N")[..6];
        var first = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "on" }, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "off" }, token);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var body = (await dup.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SystemSettingKeyDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Deactivate_marks_the_setting_inactive()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "to.remove." + Guid.NewGuid().ToString("N")[..6];
        var create = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "v" }, token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/system-settings/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/system-settings/{created.Id}", token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        Assert.False(fetched.IsActive);
    }

    // Reactivating is the update path's only route to the filtered unique index
    // on the active key, so it has to answer the create path's 409 rather than
    // let SaveChanges fail the whole request with a 500.
    [Fact]
    public async Task Reactivating_a_setting_whose_key_is_live_again_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "app.android.minVersion." + Guid.NewGuid().ToString("N")[..6];

        var first = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "1.0.0" }, token);
        var original = (await first.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;

        // Retire it, then create the replacement the filtered index permits.
        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/system-settings/{original.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var replacement = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "2.0.0" }, token);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);

        // Reopening the retired row and flipping Active back on would put two
        // live rows on one key.
        var reactivate = await PutAuthAsync(
            $"/api/v1/admin/system-settings/{original.Id}",
            new AdminUpdateSystemSettingRequest { Value = "1.0.0", IsActive = true },
            token);
        Assert.Equal(HttpStatusCode.Conflict, reactivate.StatusCode);
        var body = (await reactivate.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SystemSettingKeyDuplicate, body.Error!.Code);

        // The guard rejects, it does not half-apply.
        var get = await GetAuthAsync($"/api/v1/admin/system-settings/{original.Id}", token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        Assert.False(fetched.IsActive);
    }

    // The guard must not fire on an ordinary edit of a row that is already live.
    [Fact]
    public async Task Updating_an_active_setting_in_place_still_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "event.hotline." + Guid.NewGuid().ToString("N")[..6];
        var create = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "920000000" }, token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;

        var update = await PutAuthAsync(
            $"/api/v1/admin/system-settings/{created.Id}",
            new AdminUpdateSystemSettingRequest { Value = "920111111", IsActive = true },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        Assert.Equal("920111111", updated.Value);
        Assert.True(updated.IsActive);
    }

    // Reactivating a retired key nobody re-created is the ordinary case: still 200.
    [Fact]
    public async Task Reactivating_a_setting_whose_key_is_free_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var key = "app.ios.minVersion." + Guid.NewGuid().ToString("N")[..6];
        var create = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = key, Value = "1.0.0" }, token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        await DeleteAuthAsync($"/api/v1/admin/system-settings/{created.Id}", token);

        var reactivate = await PutAuthAsync(
            $"/api/v1/admin/system-settings/{created.Id}",
            new AdminUpdateSystemSettingRequest { Value = "1.0.1", IsActive = true },
            token);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var updated = (await reactivate.Content
            .ReadFromJsonAsync<ApiResult<AdminSystemSettingDetail>>())!.Data!;
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/system-settings",
            new AdminCreateSystemSettingRequest { Key = "nope", Value = "x" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"syscfg-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Sys Config Admin",
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
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
