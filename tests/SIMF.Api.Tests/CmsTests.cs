// D-173 (gap doc G8, PDF §1, §2.1) — Dynamic content CMS:
// ContentBlock + Banner admin CRUD + public anonymous reads.
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
using SIMF.Contracts.Cms;
using SIMF.Domain.Cms;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class CmsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public CmsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_upsert_creates_then_updates_in_place()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = "test." + Guid.NewGuid().ToString("N");

        var create = await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key,
                Content = "Welcome",
                ContentArabic = "أهلاً",
                IsActive = true,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var first = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminContentBlockSummary>>())!.Data!;
        Assert.Equal(key, first.Key);

        var update = await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key,
                Content = "Welcome back",
                ContentArabic = "أهلاً بعودتك",
                IsActive = true,
            }, admin);
        var second = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminContentBlockSummary>>())!.Data!;
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Welcome back", second.Content);
    }

    [Fact]
    public async Task Public_read_returns_active_block()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = "public." + Guid.NewGuid().ToString("N");
        await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key, Content = "Hello", ContentArabic = "مرحباً", IsActive = true,
            }, admin);

        var response = await _client.GetAsync($"/api/v1/app/content/{key}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicContentBlock>>())!.Data!;
        Assert.Equal("Hello", body.Content);
        Assert.Equal("مرحباً", body.ContentArabic);
    }

    [Fact]
    public async Task Public_read_of_inactive_block_returns_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = "hidden." + Guid.NewGuid().ToString("N");
        await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key, Content = "x", ContentArabic = "س", IsActive = false,
            }, admin);

        var response = await _client.GetAsync($"/api/v1/app/content/{key}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task If_modified_since_returns_304_when_unchanged()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = "im." + Guid.NewGuid().ToString("N");
        await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key, Content = "v1", ContentArabic = "س1", IsActive = true,
            }, admin);

        // First fetch — get the Last-Modified.
        var first = await _client.GetAsync($"/api/v1/app/content/{key}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var lastModified = first.Content.Headers.LastModified
            ?? DateTimeOffset.UtcNow;

        // Second fetch with If-Modified-Since equal to the last
        // modification time — server returns 304 with no body.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/app/content/{key}");
        request.Headers.IfModifiedSince = lastModified;
        var second = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Public_batch_returns_only_existing_active_keys()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var k1 = "batch1." + Guid.NewGuid().ToString("N");
        var k2 = "batch2." + Guid.NewGuid().ToString("N");
        await PutAuthAsync("/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest { Key = k1, Content = "a", ContentArabic = "أ", IsActive = true }, admin);
        await PutAuthAsync("/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest { Key = k2, Content = "b", ContentArabic = "ب", IsActive = true }, admin);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/content/batch",
            new PublicContentBlockBatchRequest
            {
                Keys = new List<string> { k1, k2, "does-not-exist" },
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicContentBlockBatch>>())!.Data!;
        Assert.True(body.Blocks.ContainsKey(k1));
        Assert.True(body.Blocks.ContainsKey(k2));
        Assert.False(body.Blocks.ContainsKey("does-not-exist"));
    }

    // R1 audit (#28) — an explicit {"keys": null} body (System.Text.Json
    // overwrites the initializer with null) must be a clean 400
    // (validation_failed), NOT a NullReferenceException 500. Raw JSON is used so
    // the null-Keys path is exercised regardless of client serializer settings.
    [Fact]
    public async Task Public_batch_with_null_keys_is_rejected_400()
    {
        var response = await _client.PostAsync(
            "/api/v1/app/content/batch",
            new StringContent("{\"keys\":null}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    [Fact]
    public async Task Banner_create_then_public_list_returns_active_only_within_window()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var now = DateTimeOffset.UtcNow;

        // Active banner: started yesterday, ends tomorrow.
        var liveCreate = await PostAuthAsync(
            "/api/v1/admin/banners",
            new CreateBannerRequest
            {
                Title = "Live Banner", TitleArabic = "بانر مباشر",
                Body = "Body", BodyArabic = "النص",
                Start = now.AddDays(-1), End = now.AddDays(1),
                DisplayOrder = 0,
            }, admin);
        var live = (await liveCreate.Content
            .ReadFromJsonAsync<ApiResult<AdminBannerDetail>>())!.Data!;

        // Future banner: starts tomorrow.
        await PostAuthAsync(
            "/api/v1/admin/banners",
            new CreateBannerRequest
            {
                Title = "Future Banner", TitleArabic = "بانر مستقبلي",
                Body = "Body", BodyArabic = "النص",
                Start = now.AddDays(1), End = now.AddDays(2),
                DisplayOrder = 1,
            }, admin);

        var publicList = await _client.GetAsync("/api/v1/app/banners");
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
        var body = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<PublicBanners>>())!.Data!;
        Assert.Contains(body.Items, b => b.Id == live.Id);
        Assert.DoesNotContain(body.Items, b => b.Title == "Future Banner");
    }

    [Fact]
    public async Task Banner_create_with_end_before_start_is_BANNER_INVALID_TIME_WINDOW()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var now = DateTimeOffset.UtcNow;
        var response = await PostAuthAsync(
            "/api/v1/admin/banners",
            new CreateBannerRequest
            {
                Title = "Bad", TitleArabic = "سيء",
                Body = "b", BodyArabic = "ب",
                Start = now.AddDays(1), End = now,
                DisplayOrder = 0,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BannerInvalidTimeWindow, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_content_block_upsert()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = "x", Content = "y", ContentArabic = "ص", IsActive = true,
            }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cybersecurity_policy_blocks_are_seeded_by_IdentitySeeder()
    {
        // D-174 (gap doc G11, Mockup page 39) — the Flutter mobile app
        // reads these well-known keys; their presence is a wire
        // contract enforced by the seeder.
        var keys = new[]
        {
            "cyber.title",
            "cyber.intro",
            "cyber.pillar.01.title",
            "cyber.pillar.01.body",
            "cyber.pillar.02.title",
            "cyber.pillar.03.title",
            "cyber.pillar.04.title",
            "cyber.pillar.05.title",
            "cyber.reference",
        };
        foreach (var key in keys)
        {
            var response = await _client.GetAsync($"/api/v1/app/content/{key}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = (await response.Content
                .ReadFromJsonAsync<ApiResult<PublicContentBlock>>())!.Data!;
            Assert.False(string.IsNullOrEmpty(body.Content), $"EN content missing for {key}");
            Assert.False(string.IsNullOrEmpty(body.ContentArabic), $"AR content missing for {key}");
        }
    }

    [Fact]
    public async Task Delete_content_block_makes_subsequent_public_read_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var key = "del." + Guid.NewGuid().ToString("N");
        await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key, Content = "x", ContentArabic = "س", IsActive = true,
            }, admin);

        var del = await DeleteAuthAsync(
            $"/api/v1/admin/content-blocks/{key}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var publicRead = await _client.GetAsync($"/api/v1/app/content/{key}");
        Assert.Equal(HttpStatusCode.NotFound, publicRead.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"cms-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "CMS Admin",
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
                Email = email, Password = AuthFlow.Password,
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
