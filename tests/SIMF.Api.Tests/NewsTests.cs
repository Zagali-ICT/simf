// Tests: covers PublicNewsEndpoints + AdminNewsEndpoints (D-199 News module).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>D-199 — News module. Covers admin CRUD (gated by the
/// PublicRelations/Administrator policy), the unique-English-title 409,
/// soft-delete + publish-window filtering on the public feed, and the
/// anonymous public read contract. Mirrors AdminArchiveTests / DelegationsTests.</summary>
public sealed class NewsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NewsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static CreateNewsRequest SampleCreate(string titleEn, DateTimeOffset? publishedAt = null) => new()
    {
        TitleEn = titleEn,
        TitleAr = "خبر",
        ExcerptEn = "Short teaser.",
        ExcerptAr = "مقتطف قصير.",
        BodyEn = "Full article body text goes here.",
        BodyAr = "نص الخبر الكامل هنا.",
        CategoryEn = "NAVAL",
        CategoryAr = "بحرية",
        ImageRelativePath = "news/feature.jpg",
        PublishedAt = publishedAt ?? DateTimeOffset.UtcNow.AddMinutes(-5),
        DisplayOrder = 1,
    };

    [Fact]
    public async Task Admin_create_then_get_roundtrips()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var create = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("RSNF commissions new frigate"), token);
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await create.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();
        created!.Success.Should().BeTrue();
        created.Data!.TitleEn.Should().Be("RSNF commissions new frigate");

        var get = await GetAuthAsync($"/api/v1/admin/news/{created.Data.Id}", token);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();
        fetched!.Data!.BodyEn.Should().Be("Full article body text goes here.");
        fetched.Data.ExcerptEn.Should().Be("Short teaser.");
    }

    [Fact]
    public async Task Duplicate_english_title_returns_conflict()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var first = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Opening ceremony schedule released"), token);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var dup = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Opening ceremony schedule released"), token);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_can_soft_delete_the_article()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Editable article"), token);
        var created = await create.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();
        var id = created!.Data!.Id;

        var del = await DeleteAuthAsync($"/api/v1/admin/news/{id}", token);
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin GET still sees the soft-deleted row.
        var afterDelete = await GetAuthAsync($"/api/v1/admin/news/{id}", token);
        var deleted = await afterDelete.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();
        deleted!.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Public_feed_returns_published_active_article()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Speaker line-up announced"), token);
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetAsync("/api/v1/news?page=1&pageSize=20");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await list.Content.ReadFromJsonAsync<ApiResult<PublicNewsPage>>();
        page!.Success.Should().BeTrue();
        page.Data!.Items.Should().Contain(i => i.TitleEn == "Speaker line-up announced");
    }

    [Fact]
    public async Task Public_feed_excludes_future_published()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Embargoed announcement", DateTimeOffset.UtcNow.AddDays(7)), token);
        var created = await create.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();

        var list = await _client.GetAsync("/api/v1/news");
        var page = await list.Content.ReadFromJsonAsync<ApiResult<PublicNewsPage>>();
        page!.Data!.Items.Should().NotContain(i => i.TitleEn == "Embargoed announcement");

        // The public detail endpoint hides the not-yet-published article too.
        var detail = await _client.GetAsync($"/api/v1/news/{created!.Data!.Id}");
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_feed_excludes_soft_deleted()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/news",
            SampleCreate("Press accreditation now open"), token);
        var created = await create.Content.ReadFromJsonAsync<ApiResult<AdminNewsDetail>>();

        var del = await DeleteAuthAsync($"/api/v1/admin/news/{created!.Data!.Id}", token);
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await _client.GetAsync("/api/v1/news");
        var page = await list.Content.ReadFromJsonAsync<ApiResult<PublicNewsPage>>();
        page!.Data!.Items.Should().NotContain(i => i.TitleEn == "Press accreditation now open");
    }

    [Fact]
    public async Task Admin_endpoints_require_authentication()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/admin/news/list", new GridQuery());
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- helpers (mirror AdminArchiveTests) --

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-news-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Admin News",
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
                Email = email, Password = AuthFlow.Password,
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
