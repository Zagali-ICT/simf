// D-199 (Mockup page 30) — anonymous public media gallery: active only,
// paged, by album, plus the out-of-row image stream.
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

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class PublicMediaTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PublicMediaTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_active_only_ordered_by_displayorder()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var album = $"PubAlbum-{Guid.NewGuid():N}";

        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "second", Album = album, DisplayOrder = 20 }, token);
        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "first", Album = album, DisplayOrder = 10 }, token);
        var inactive = await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "hidden", Album = album, DisplayOrder = 1 }, token);
        var inactiveId = (await inactive.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!.Id;
        var del = await DeleteAuthAsync($"/api/v1/admin/media/{inactiveId}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        // Anonymous read (no Authorization header).
        var resp = await _client.GetAsync($"/api/v1/app/media?album={album}&top=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!;
        Assert.True(page.Success);

        Assert.Equal(2, page.Data!.Items.Count);
        Assert.DoesNotContain(page.Data.Items, i => i.Id == inactiveId);
        Assert.Equal("first", page.Data.Items[0].Title);
        Assert.Equal("second", page.Data.Items[1].Title);
    }

    [Fact]
    public async Task Album_filter_narrows_results()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var albumA = $"A-{Guid.NewGuid():N}";
        var albumB = $"B-{Guid.NewGuid():N}";

        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "in-a", Album = albumA, DisplayOrder = 1 }, token);
        await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "in-b", Album = albumB, DisplayOrder = 1 }, token);

        var resp = await _client.GetAsync($"/api/v1/app/media?album={albumA}&top=100");
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!.Data!;
        Assert.All(page.Items, i => Assert.Equal(albumA, i.Album));
        Assert.Contains(page.Items, i => i.Title == "in-a");
        Assert.DoesNotContain(page.Items, i => i.Title == "in-b");
    }

    [Fact]
    public async Task Kind_filter_narrows_to_the_requested_kind()
    {
        // A8 — ?kind= narrows the gallery to one MediaKind; absent = both kinds.
        var token = await CreateAdministratorAndSignInAsync();
        var album = $"K-{Guid.NewGuid():N}";

        var imageCreate = await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest
            { Kind = MediaKind.Image, Title = "an-image", Album = album, DisplayOrder = 1 }, token);
        Assert.Equal(HttpStatusCode.OK, imageCreate.StatusCode);
        // A video media item requires a playback URL (admin-create validation).
        var videoCreate = await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest
            {
                Kind = MediaKind.Video, Title = "a-video", Album = album,
                Url = "https://youtu.be/abc123", DisplayOrder = 2,
            }, token);
        Assert.Equal(HttpStatusCode.OK, videoCreate.StatusCode);

        // ?kind=Video → only the video.
        var videosResp = await _client.GetAsync(
            $"/api/v1/app/media?album={album}&kind=Video&top=100");
        var videos = (await videosResp.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!.Data!;
        Assert.All(videos.Items, i => Assert.Equal(MediaKind.Video, i.Kind));
        Assert.Contains(videos.Items, i => i.Title == "a-video");
        Assert.DoesNotContain(videos.Items, i => i.Title == "an-image");

        // No ?kind= → both kinds are returned (backward-compatible default).
        var allResp = await _client.GetAsync($"/api/v1/app/media?album={album}&top=100");
        var all = (await allResp.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!.Data!;
        Assert.Contains(all.Items, i => i.Title == "an-image");
        Assert.Contains(all.Items, i => i.Title == "a-video");
    }

    [Fact]
    public async Task Image_url_is_null_when_no_bytes_and_stream_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var album = $"NoBytes-{Guid.NewGuid():N}";
        var create = await PostAuthAsync("/api/v1/admin/media",
            new AdminCreateMediaRequest { Kind = MediaKind.Image, Title = "no-bytes", Album = album, DisplayOrder = 1 }, token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminMediaDetail>>())!.Data!.Id;

        var resp = await _client.GetAsync($"/api/v1/app/media?album={album}&top=100");
        var page = (await resp.Content
            .ReadFromJsonAsync<ApiResult<PublicMediaPage>>())!.Data!;
        var item = Assert.Single(page.Items, i => i.Id == id);
        Assert.Null(item.ImageUrl);

        var img = await _client.GetAsync($"/api/v1/app/media/{id}/image");
        Assert.Equal(HttpStatusCode.NotFound, img.StatusCode);
    }

    [Fact]
    public async Task Image_stream_404_for_unknown_id()
    {
        var resp = await _client.GetAsync($"/api/v1/app/media/{Guid.NewGuid()}/image");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"media-pub-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Media Pub Admin",
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
