// Tests: SIMF.Api/Endpoints/PublicRelations/MediaPartnerEndpoints.cs
// D-199 (Mockup page 31) — the public (anonymous) media-partner list endpoint.
// Mirrors the public-read shape; admin seeding goes through the /api/v1 admin
// CRUD with an Administrator token (created the same way as AdminCountriesTests).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class MediaPartnersTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MediaPartnersTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/app/media-partners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPartners>>())!;
        Assert.True(payload.Success);
        Assert.NotNull(payload.Data!.Items);
    }

    [Fact]
    public async Task Public_list_shows_active_partner_and_hides_it_after_soft_delete()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                $"Public Visible {Guid.NewGuid():N}", "شبكة ظاهرة للعموم",
                "https://pvn.example", 4100),
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;

        // Active -> appears on the anonymous public list.
        var before = await _client.GetAsync("/api/v1/app/media-partners");
        var beforeItems = (await before.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPartners>>())!.Data!.Items;
        Assert.Contains(beforeItems, p => p.Id == id);

        // Soft-delete -> drops off the public list.
        var delete = await DeleteAuthAsync($"/api/v1/admin/media-partners/{id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var after = await _client.GetAsync("/api/v1/app/media-partners");
        var afterItems = (await after.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPartners>>())!.Data!.Items;
        Assert.DoesNotContain(afterItems, p => p.Id == id);
    }

    [Fact]
    public async Task Public_list_is_ordered_by_display_order_then_arabic_name()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var low = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                $"Order Low {Guid.NewGuid():N}", "ترتيب منخفض", null, 4200),
            token);
        var high = await PostAuthAsync(
            "/api/v1/admin/media-partners",
            new AdminCreateMediaPartnerRequest(
                $"Order High {Guid.NewGuid():N}", "ترتيب مرتفع", null, 4300),
            token);

        var lowId = (await low.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;
        var highId = (await high.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaPartnerDetail>>())!.Data!.Id;

        var response = await _client.GetAsync("/api/v1/app/media-partners");
        var items = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPartners>>())!.Data!.Items;

        var lowIndex = IndexOf(items, lowId);
        var highIndex = IndexOf(items, highId);
        Assert.True(lowIndex >= 0);
        Assert.True(highIndex >= 0);
        Assert.True(lowIndex < highIndex);
    }

    private static int IndexOf(IReadOnlyList<PublicMediaPartnerItem> items, Guid id)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Id == id) { return i; }
        }
        return -1;
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"media-public-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Media Public Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
