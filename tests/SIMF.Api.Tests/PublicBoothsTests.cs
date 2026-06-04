// D-199 — public anonymous Booth reads (Mockup page 22 + the 2D venue map).
// Mirrors the public Delegations read tests in DelegationsTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PublicBoothsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicBoothsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_succeeds()
    {
        var response = await _client.GetAsync("/api/v1/app/booths");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!;
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
    }

    [Fact]
    public async Task Created_active_booth_appears_in_public_list_and_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = code,
                Name = "Maritime Security Solutions",
                NameArabic = "حلول الأمن البحري",
                Sector = "Defense Systems",
                MapX = 3.0,
                MapY = 4.0,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var list = await _client.GetAsync("/api/v1/app/booths");
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        Assert.Contains(rows, b => b.Id == created.Id && b.Code == code);

        var detailResponse = await _client.GetAsync($"/api/v1/app/booths/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<PublicBoothDetail>>())!.Data!;
        Assert.Equal(created.Id, detail.Id);
        Assert.Equal(3.0, detail.MapX);
    }

    [Fact]
    public async Task Public_detail_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/v1/app/booths/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_booth_is_absent_from_public_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, Name = "Hidden", NameArabic = "مخفي" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var detailResponse = await _client.GetAsync($"/api/v1/app/booths/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"booth-pub-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Booth Pub Admin",
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
                Email = email,
                Password = AuthFlow.Password,
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
