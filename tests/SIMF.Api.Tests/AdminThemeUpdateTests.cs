// Tests: SIMF.Api/Endpoints/Admin/ThemeEndpoints.cs (UpdateThemeEndpoint)
//
// D-844 — `PUT /api/v1/admin/themes/{id}` had NO test of any kind. ThemeEndpoints.cs
// carried `// Tests: SIMF.Api.Tests/AdminThemesTests.cs`, a file that does not exist
// anywhere in the repository; the only theme coverage was ThemesExcelTests and a
// permission check on the LIST endpoint.
//
// Written before converting the endpoint's route DTO to the D-505 inheriting shape,
// so the conversion is verifiable rather than merely plausible. Every field is
// asserted individually: the failure this programme keeps finding is ONE field
// quietly not arriving, which a test that only checks Name would sail past.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminThemeUpdateTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminThemeUpdateTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Every_field_on_a_theme_round_trips_through_an_update()
    {
        // Each assertion below is a field that a dropped mapping line would
        // silently revert. Asserted from a RE-READ of the row, so a response
        // composed in memory cannot mask a column that never changed.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = $"TH{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var themeId = await CreateThemeAsync(adminToken, code);

        var newCode = $"TH{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        using var response = await PutAuthAsync(
            $"/api/v1/admin/themes/{themeId}",
            new AdminUpdateThemeRequest
            {
                Code = newCode,
                Name = "Maritime Security (edited)",
                NameArabic = "الأمن البحري (معدل)",
                Description = "Edited description",
                DescriptionArabic = "وصف معدل",
                DisplayOrder = 7,
                PageColor = "#123456",
                IsActive = false,
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await FindThemeAsync(adminToken, themeId);
        Assert.NotNull(stored);
        Assert.Equal(newCode, stored!.Code);
        Assert.Equal("Maritime Security (edited)", stored.Name);
        Assert.Equal("الأمن البحري (معدل)", stored.NameArabic);
        Assert.Equal("Edited description", stored.Description);
        Assert.Equal("وصف معدل", stored.DescriptionArabic);
        Assert.Equal(7, stored.DisplayOrder);
        Assert.Equal("#123456", stored.PageColor);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task An_update_that_changes_one_field_leaves_the_others_alone()
    {
        // The other half of the same risk: a PUT that carries every field must
        // not disturb the ones the admin did not touch.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = $"TH{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var themeId = await CreateThemeAsync(adminToken, code);
        var before = await FindThemeAsync(adminToken, themeId);
        Assert.NotNull(before);

        using var response = await PutAuthAsync(
            $"/api/v1/admin/themes/{themeId}",
            new AdminUpdateThemeRequest
            {
                Code = before!.Code,
                Name = "Only the name moved",
                NameArabic = before.NameArabic,
                Description = before.Description,
                DescriptionArabic = before.DescriptionArabic,
                DisplayOrder = before.DisplayOrder,
                PageColor = before.PageColor,
                IsActive = before.IsActive,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await FindThemeAsync(adminToken, themeId);
        Assert.NotNull(after);
        Assert.Equal("Only the name moved", after!.Name);
        Assert.Equal(before.Code, after.Code);
        Assert.Equal(before.NameArabic, after.NameArabic);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.DescriptionArabic, after.DescriptionArabic);
        Assert.Equal(before.DisplayOrder, after.DisplayOrder);
        Assert.Equal(before.PageColor, after.PageColor);
        Assert.Equal(before.IsActive, after.IsActive);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateThemeAsync(string token, string code)
    {
        using var response = await PostAuthAsync(
            "/api/v1/admin/themes",
            new AdminCreateThemeRequest
            {
                Code = code,
                Name = "Maritime Security",
                NameArabic = "الأمن البحري",
                Description = "Original description",
                DescriptionArabic = "الوصف الأصلي",
                DisplayOrder = 3,
                PageColor = "#0B7285",
            }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminThemeSummary>>())!.Data!;
        return summary.Id;
    }

    private async Task<AdminThemeSummary?> FindThemeAsync(string token, Guid id)
    {
        using var response = await PostAuthAsync(
            "/api/v1/admin/themes/list", new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminThemeSummary>>>())!;
        return body.Data!.Items.FirstOrDefault(row => row.Id == id);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"theme-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Theme Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class =>
        SendAuthAsync(HttpMethod.Post, url, body, token);

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class =>
        SendAuthAsync(HttpMethod.Put, url, body, token);

    private Task<HttpResponseMessage> SendAuthAsync<TBody>(
        HttpMethod method, string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
