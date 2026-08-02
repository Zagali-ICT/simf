// D-768 — the CP-uploaded hero background video served from our own API (streamed
// store + anonymous Range serve). Covers upload → BackgroundVideoUrl points at the
// served .mp4 route, anonymous full + range streaming, the non-video + non-admin
// rejections, and remove → stream 404 + URL cleared. The hero video is a SINGLETON,
// so each test establishes its own state (upload / delete) up front.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Organization;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class OrganizationHeroVideoTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string StreamPath = "/api/v1/app/organization/hero-video.mp4";
    private const string UploadPath = "/api/v1/admin/organization-profile/hero-video";
    private static readonly byte[] SampleVideo =
        "FAKE-MP4-HERO-VIDEO-BYTES-0123456789"u8.ToArray();

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public OrganizationHeroVideoTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_points_background_url_at_the_served_route()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var profile = await UploadHeroVideoAsync(token);

        Assert.NotNull(profile.BackgroundVideoUrl);
        // Config PublicApiBaseUrl is blank in tests, so the URL is request-derived —
        // assert the load-bearing .mp4 route suffix the hero accept-gate keys on.
        Assert.EndsWith(StreamPath, profile.BackgroundVideoUrl);
    }

    [Fact]
    public async Task Public_stream_returns_the_uploaded_bytes_anonymously()
    {
        var token = await CreateAdministratorAndSignInAsync();
        await UploadHeroVideoAsync(token);

        // No Authorization header — the hero video is public branding content.
        var response = await _client.GetAsync(StreamPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SampleVideo, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("nosniff", response.Headers.TryGetValues("X-Content-Type-Options", out var v)
            ? string.Join(",", v) : string.Empty);
    }

    [Fact]
    public async Task Public_stream_supports_http_range_requests()
    {
        var token = await CreateAdministratorAndSignInAsync();
        await UploadHeroVideoAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Get, StreamPath);
        request.Headers.Range = new RangeHeaderValue(0, 3);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(SampleVideo[..4], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_of_a_non_video_file_is_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, UploadPath)
        {
            Content = BuildForm("malicious.html", "text/html"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OrganizationProfileInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_cannot_upload()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var request = new HttpRequestMessage(HttpMethod.Post, UploadPath)
        {
            Content = BuildForm(),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_video_and_clears_the_url()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var uploaded = await UploadHeroVideoAsync(token);
        var servedUrl = uploaded.BackgroundVideoUrl;

        var delete = await DeleteAuthAsync(UploadPath, token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var profile = (await delete.Content
            .ReadFromJsonAsync<ApiResult<OrganizationProfileResponse>>())!.Data!;
        // The URL pointed at our served route, so remove clears it.
        Assert.NotEqual(servedUrl, profile.BackgroundVideoUrl);
        Assert.Null(profile.BackgroundVideoUrl);

        // The stream now 404s (no active hero video).
        var stream = await _client.GetAsync(StreamPath);
        Assert.Equal(HttpStatusCode.NotFound, stream.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<OrganizationProfileResponse> UploadHeroVideoAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, UploadPath)
        {
            Content = BuildForm(),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<OrganizationProfileResponse>>())!.Data!;
    }

    private static MultipartFormDataContent BuildForm(
        string fileName = "hero.mp4", string contentType = "video/mp4")
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(SampleVideo);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"hero-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Hero Video Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
