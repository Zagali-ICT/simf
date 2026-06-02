// D-199 (Mockup page 30 — معرض الصور والفيديوهات) — admin CRUD over MediaItem.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Media;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminMediaTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminMediaTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_returns_the_item()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest
            {
                Kind = MediaKind.Image,
                TitleEn = "Opening ceremony",
                TitleAr = "حفل الافتتاح",
                AlbumEn = "Day 1",
                AlbumAr = "اليوم الأول",
                DisplayOrder = 5,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!;
        Assert.Equal(MediaKind.Image, created.Kind);
        Assert.False(created.HasImage);

        var get = await GetAuthAsync($"/api/v1/admin/media/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!;
        Assert.Equal("Opening ceremony", fetched.TitleEn);
        Assert.Equal("Day 1", fetched.AlbumEn);
    }

    [Fact]
    public async Task Video_without_url_is_400_MEDIA_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest
            {
                Kind = MediaKind.Video,
                TitleEn = "Highlights",
                DisplayOrder = 1,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        var body = (await create.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal("media_invalid", body.Error!.Code);
    }

    [Fact]
    public async Task Update_then_soft_delete_drops_from_public_list()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var album = $"Sessions-{Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest
            {
                Kind = MediaKind.Video,
                TitleEn = "Panel",
                Url = "https://example.com/panel.mp4",
                AlbumEn = album,
                DisplayOrder = 3,
            },
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!.Id;

        var update = await PutAuthAsync(
            $"/api/v1/admin/media/{id}",
            new AdminUpdateMediaRequest
            {
                Kind = MediaKind.Video,
                TitleEn = "Panel (edited)",
                Url = "https://example.com/panel.mp4",
                AlbumEn = album,
                DisplayOrder = 4,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var del = await DeleteAuthAsync($"/api/v1/admin/media/{id}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        // Public list must not surface the soft-deleted item.
        var list = await _client.GetAsync($"/api/v1/app/media?album={album}&top=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!;
        Assert.DoesNotContain(page.Data!.Items, i => i.Id == id);
    }

    [Fact]
    public async Task Deactivate_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "I", DisplayOrder = 1 },
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!.Id;

        var first = await DeleteAuthAsync($"/api/v1/admin/media/{id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await DeleteAuthAsync($"/api/v1/admin/media/{id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var read = await GetAuthAsync($"/api/v1/admin/media/{id}", token);
        var afterDetail = (await read.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!;
        Assert.False(afterDetail.IsActive);
    }

    [Fact]
    public async Task List_returns_a_page()
    {
        var token = await CreateAdministratorAndSignInAsync();
        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "A", DisplayOrder = 20 }, token);
        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "B", DisplayOrder = 10 }, token);

        var list = await PostAuthAsync("/api/v1/admin/media/list",
            new GridQuery { Top = 100 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMediaSummary>>>())!.Data!;
        Assert.True(page.Items.Count >= 2);
    }

    [Fact]
    public async Task Image_upload_sets_HasImage_and_public_image_streams()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var album = $"Img-{Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "withbytes", AlbumEn = album, DisplayOrder = 1 },
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!.Id;

        // Minimal 1x1 PNG.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(png);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "File", "tile.png");

        var upload = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/media/{id}/image") { Content = form };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResp = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);
        var afterUpload = (await uploadResp.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!;
        Assert.True(afterUpload.HasImage);

        // Public (anonymous) image stream now returns the bytes.
        var img = await _client.GetAsync($"/api/v1/app/media/{id}/image");
        Assert.Equal(HttpStatusCode.OK, img.StatusCode);
        var bytes = await img.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        // And the public list now carries the image url.
        var list = await _client.GetAsync($"/api/v1/app/media?album={album}&top=100");
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!.Data!;
        Assert.Contains(page.Items, i => i.Id == id && i.ImageUrl != null);
    }

    [Fact]
    public async Task Anonymous_caller_is_unauthorized_on_create()
    {
        var resp = await _client.PostAsJsonAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "X", DisplayOrder = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var resp = await PostAuthAsync(
            "/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, TitleEn = "Y", DisplayOrder = 1 },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await GetAuthAsync($"/api/v1/admin/media/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"media-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Media Admin",
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
        var body = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
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
