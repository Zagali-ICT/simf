// D-357 — integration coverage for the unified media-asset endpoint family:
// per-category upload / external-link / public-serve gated by the OWNING entity's
// permission, plus the central Media Library management (list / deactivate /
// restore) gated by MediaLibrary.View / .Manage. Owner ids are free Guids — the
// asset rows reference them polymorphically with no FK (D-157) — but the
// ANONYMOUS public serve now refuses a soft-deleted owner (A9), so the positive
// serve tests seed an ACTIVE owner row for the owner id; the admin preview does
// not (it may show a deactivated owner's asset).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AssetEndpointsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // Minimal 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AssetEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_image_then_public_app_image_streams()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await SeedActiveSpeakerAsync(owner);

        var upload = await UploadAsync("SpeakerPhoto", owner, "Image", Png, "image/png", "p.png", token);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        // Anonymous public serve now returns the bytes.
        var img = await _client.GetAsync($"/api/v1/app/assets/SpeakerPhoto/{owner}/image");
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        Assert.NotEmpty(await img.Content.ReadAsByteArrayAsync());

        // And it shows up in the Media Library list as an active uploaded image.
        var row = await FindByOwnerAsync(owner, token);
        Assert.NotNull(row);
        Assert.Equal(AssetCategory.SpeakerPhoto, row!.Category);
        Assert.Equal(AssetKind.Image, row.Kind);
        Assert.Equal(AssetSourceType.Upload, row.SourceType);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task Public_app_image_for_a_missing_asset_returns_404()
    {
        // Probe: the anonymous serve of an owner with no asset must be 404 (not a
        // FastEndpoints auto-204). Exercises AssetAuth.ServeAsync's null-resolution
        // branch, which no prior test covered.
        var missing = await _client.GetAsync(
            $"/api/v1/app/assets/SpeakerPhoto/{Guid.NewGuid()}/image");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Public_app_image_for_an_external_link_returns_302()
    {
        // The external-link asset serve must 302-redirect to the stored URL (not a
        // FastEndpoints auto-204). A non-redirecting client observes the 302 itself
        // rather than following it. Exercises AssetAuth.ServeAsync's link branch.
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await SeedNewsAsync(owner, publishedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var link = await PutAuthAsync(
            $"/api/v1/admin/assets/NewsImage/{owner}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://cdn.example/x.jpg" },
            token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        using var noRedirect = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var img = await noRedirect.GetAsync($"/api/v1/app/assets/NewsImage/{owner}/image");
        Assert.Equal(HttpStatusCode.Redirect, img.StatusCode); // 302 Found
        Assert.Equal("https://cdn.example/x.jpg", img.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Public_app_image_returns_304_for_a_matching_if_none_match()
    {
        // A6 — the asset serve emits a strong content-hash ETag: a conditional
        // re-fetch with a matching If-None-Match gets 304 (no body); a non-matching
        // validator gets 200 + bytes. A plain first fetch is unchanged (200 + ETag).
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await SeedActiveSpeakerAsync(owner);
        var upload = await UploadAsync(
            "SpeakerPhoto", owner, "Image", Png, "image/png", "p.png", token);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var url = $"/api/v1/app/assets/SpeakerPhoto/{owner}/image";

        // First fetch: 200 + a non-empty ETag header.
        var first = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        Assert.False(string.IsNullOrEmpty(etag!.Tag));

        // Conditional re-fetch with the matching validator → 304 + empty body.
        var conditional = new HttpRequestMessage(HttpMethod.Get, url);
        conditional.Headers.IfNoneMatch.Add(etag);
        var revalidated = await _client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, revalidated.StatusCode);
        Assert.Empty(await revalidated.Content.ReadAsByteArrayAsync());

        // A non-matching validator → the full 200 + bytes.
        var mismatch = new HttpRequestMessage(HttpMethod.Get, url);
        mismatch.Headers.TryAddWithoutValidation("If-None-Match", "\"deadbeef\"");
        var full = await _client.SendAsync(mismatch);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.NotEmpty(await full.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Public_serve_stops_after_the_owner_is_soft_deleted_but_admin_preview_does_not()
    {
        // A9 (security) — a deactivated owner drops off every public list, but the
        // serve URL is a deterministic (category, ownerId). After the owner is
        // soft-deleted the ANONYMOUS serve must 404, while the gated admin preview
        // still streams (the Media Library manages deactivated owners' assets).
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await SeedActiveSpeakerAsync(owner);
        var upload = await UploadAsync("SpeakerPhoto", owner, "Image", Png, "image/png", "p.png", token);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var publicUrl = $"/api/v1/app/assets/SpeakerPhoto/{owner}/image";
        var adminUrl = $"/api/v1/admin/assets/SpeakerPhoto/{owner}/image";

        // While the owner is active, both the public serve and the admin preview
        // stream the bytes.
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(publicUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAuthAsync(adminUrl, token)).StatusCode);

        // Soft-delete the owning speaker (the asset row itself stays IsActive).
        await DeactivateSpeakerAsync(owner);

        // Anonymous public serve now refuses (404); admin preview still streams.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(publicUrl)).StatusCode);
        var adminAfter = await GetAuthAsync(adminUrl, token);
        Assert.Equal(HttpStatusCode.OK, adminAfter.StatusCode);
        Assert.NotEmpty(await adminAfter.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Public_serve_refuses_an_embargoed_news_image()
    {
        // A9 (security) — News is public only when IsActive AND PublishedAt <= now.
        // An embargoed/scheduled article (future PublishedAt) is hidden from the
        // feed, so its hero image must also 404 on the anonymous serve — the
        // owner-active check mirrors the FULL public gate, not just IsActive.
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await SeedNewsAsync(owner, publishedAt: DateTimeOffset.UtcNow.AddDays(7)); // embargoed
        var link = await PutAuthAsync(
            $"/api/v1/admin/assets/NewsImage/{owner}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://cdn.example/embargo.jpg" },
            token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        using var noRedirect = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var img = await noRedirect.GetAsync($"/api/v1/app/assets/NewsImage/{owner}/image");
        // Embargoed → not served publicly (would have been a 302 to the link).
        Assert.Equal(HttpStatusCode.NotFound, img.StatusCode);

        // But the gated admin preview still resolves it (requireOwnerActive=false).
        using var adminClient = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var adminReq = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/assets/NewsImage/{owner}/image");
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var adminResp = await adminClient.SendAsync(adminReq);
        Assert.Equal(HttpStatusCode.Redirect, adminResp.StatusCode); // 302 to the link
    }

    [Fact]
    public async Task External_link_is_stored_as_a_link_row()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        var link = await PutAuthAsync(
            $"/api/v1/admin/assets/NewsImage/{owner}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = "https://cdn.example/x.jpg" },
            token);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        var row = await FindByOwnerAsync(owner, token);
        Assert.NotNull(row);
        Assert.Equal(AssetSourceType.ExternalLink, row!.SourceType);
        Assert.Equal("https://cdn.example/x.jpg", row.ExternalUrl);
    }

    [Fact]
    public async Task Deactivate_then_restore_round_trips()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        await UploadAsync("SpeakerPhoto", owner, "Image", Png, "image/png", "p.png", token);
        var id = (await FindByOwnerAsync(owner, token))!.Id;

        var del = await DeleteAuthAsync($"/api/v1/admin/assets/item/{id}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.False((await GetItemAsync(id, token))!.IsActive);

        var restore = await PostAuthAsync($"/api/v1/admin/assets/item/{id}/restore", new { }, token);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.True((await GetItemAsync(id, token))!.IsActive);
    }

    [Fact]
    public async Task Restore_is_blocked_with_409_when_a_live_asset_owns_the_pair()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        // Asset A, then deactivate it.
        await UploadAsync("SpeakerPhoto", owner, "Image", Png, "image/png", "a.png", token);
        var idA = (await FindByOwnerAsync(owner, token))!.Id;
        await DeleteAuthAsync($"/api/v1/admin/assets/item/{idA}", token);

        // Asset B for the same (category, owner) — a new live row.
        await UploadAsync("SpeakerPhoto", owner, "Image", Png, "image/png", "b.png", token);

        // Restoring A now collides with the live B → 409 (the filtered unique index).
        var restore = await PostAuthAsync($"/api/v1/admin/assets/item/{idA}/restore", new { }, token);
        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
        Assert.False((await GetItemAsync(idA, token))!.IsActive);
    }

    [Fact]
    public async Task Wrong_content_type_upload_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        var resp = await UploadAsync(
            "SpeakerPhoto", owner, "Image", "not-an-image"u8.ToArray(), "text/plain", "x.txt", token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Video_kind_upload_is_rejected_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        var resp = await UploadAsync("SpeakerPhoto", owner, "Video", Png, "image/png", "v.png", token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_caller_is_unauthorized_on_upload()
    {
        var owner = Guid.NewGuid();
        using var form = BuildForm(Png, "image/png", "p.png");
        var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/assets/SpeakerPhoto/{owner}/image?kind=Image") { Content = form };
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_upload()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var owner = Guid.NewGuid();
        var resp = await UploadAsync(
            "SpeakerPhoto", owner, "Image", Png, "image/png", "p.png", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Media_library_list_is_forbidden_without_the_permission()
    {
        // A visitor is approved (passes RequireApprovedAccount) but holds no
        // MediaLibrary.View claim → 403 on the management list.
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var resp = await PostAuthAsync("/api/v1/admin/assets/list", new GridQuery { Top = 10 }, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_item_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await GetAuthAsync($"/api/v1/admin/assets/item/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    // A9 — seed an ACTIVE owning entity so the anonymous public serve (which now
    // refuses a soft-deleted owner) streams the asset. Timestamps are stamped by
    // the audit interceptor on add.
    private async Task SeedActiveSpeakerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.Speakers.Add(new Speaker
        {
            Id = id,
            Code = id.ToString("N")[..16], // unique, within the 16-char Code cap
            Name = "Serve Owner",
            NameArabic = "المالك",
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    // News is public only when IsActive AND PublishedAt <= now, so the serve gate
    // mirrors both. publishedAt in the future = an embargoed article (feed-hidden).
    private async Task SeedNewsAsync(Guid id, DateTimeOffset publishedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.News.Add(new News
        {
            Id = id,
            Title = "Serve Owner",
            TitleArabic = "المالك",
            Body = "b",
            BodyArabic = "ب",
            Category = "NEWS",
            CategoryArabic = "أخبار",
            IsActive = true,
            PublishedAt = publishedAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task DeactivateSpeakerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = await db.Speakers.FirstAsync(x => x.Id == id);
        speaker.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static MultipartFormDataContent BuildForm(byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);
        return form;
    }

    private Task<HttpResponseMessage> UploadAsync(
        string category, Guid owner, string kind, byte[] bytes, string contentType, string fileName, string token)
    {
        var form = BuildForm(bytes, contentType, fileName);
        var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/assets/{category}/{owner}/image?kind={kind}") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private async Task<AdminAssetSummary?> FindByOwnerAsync(Guid owner, string token)
    {
        var resp = await PostAuthAsync("/api/v1/admin/assets/list", new GridQuery { Top = 200 }, token);
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminAssetSummary>>>())!.Data!;
        return page.Items.FirstOrDefault(a => a.OwnerId == owner);
    }

    private async Task<AdminAssetSummary?> GetItemAsync(Guid id, string token)
    {
        var resp = await GetAuthAsync($"/api/v1/admin/assets/item/{id}", token);
        if (resp.StatusCode != HttpStatusCode.OK) { return null; }
        return (await resp.Content.ReadFromJsonAsync<ApiResult<AdminAssetSummary>>())!.Data;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"asset-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Asset Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
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
