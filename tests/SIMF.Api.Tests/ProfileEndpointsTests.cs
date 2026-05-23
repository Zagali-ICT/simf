using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the account-profile and avatar endpoints
/// (myComment item #11).
/// </summary>
public sealed class ProfileEndpointsTests : IClassFixture<SimfApiFactory>
{
    private static readonly byte[] OnePixelPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ProfileEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProfile_returns_the_signed_in_users_details()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync("/api/v1/account/profile", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<ProfileResponse>>())!;
        Assert.True(body.Success);
        Assert.Equal(tokens.User.Email, body.Data!.Email);
        Assert.False(body.Data.TwoFactorEnabled);
        Assert.Null(body.Data.AvatarDataUri);
        Assert.Empty(body.Data.Roles);
    }

    [Fact]
    public async Task GetProfile_without_a_bearer_token_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/account/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_sets_the_avatar_and_GetProfile_returns_the_data_uri()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var upload = await UploadAvatarAsync(OnePixelPng, "image/png", "avatar.png", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var profileResponse = await GetAuthAsync("/api/v1/account/profile", tokens.AccessToken);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<ApiResult<ProfileResponse>>())!;
        Assert.StartsWith("data:image/png;base64,", profile.Data!.AvatarDataUri);
    }

    [Fact]
    public async Task Upload_rejects_an_unsupported_mime_type()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await UploadAvatarAsync(
            OnePixelPng, "application/pdf", "avatar.pdf", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AvatarMimeUnsupported, body.Error!.Code);
    }

    [Fact]
    public async Task Upload_rejects_a_file_larger_than_two_megabytes()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var oversized = new byte[(2 * 1024 * 1024) + 1];

        var response = await UploadAvatarAsync(
            oversized, "image/png", "avatar.png", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AvatarFileTooLarge, body.Error!.Code);
    }

    [Fact]
    public async Task Upload_rejects_an_empty_file_part()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await UploadAvatarAsync(
            [], "image/png", "empty.png", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AvatarFileMissing, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));
    }

    [Fact]
    public async Task Upload_rejects_a_payload_whose_bytes_do_not_match_the_declared_mime()
    {
        // PDF-shaped bytes ("%PDF-") with Content-Type lying as image/png.
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await UploadAvatarAsync(
            pdfBytes, "image/png", "fake.png", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AvatarMimeUnsupported, body.Error!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Error.MessageArabic));
    }

    [Fact]
    public async Task Upload_accepts_a_mime_type_in_any_case()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        // "Image/PNG" with mixed casing — the allowlist check is case-insensitive.
        var response = await UploadAvatarAsync(
            OnePixelPng, "Image/PNG", "avatar.png", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_replaces_an_existing_avatar()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        await UploadAvatarAsync(OnePixelPng, "image/png", "first.png", tokens.AccessToken);

        // A second upload with the same bytes should succeed — the new
        // avatar replaces the old (no duplicate-record error).
        var response = await UploadAvatarAsync(
            OnePixelPng, "image/png", "second.png", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = (await (await GetAuthAsync("/api/v1/account/profile", tokens.AccessToken))
            .Content.ReadFromJsonAsync<ApiResult<ProfileResponse>>())!;
        Assert.StartsWith("data:image/png;base64,", profile.Data!.AvatarDataUri);
    }

    [Fact]
    public async Task Delete_clears_the_avatar()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        await UploadAvatarAsync(OnePixelPng, "image/png", "avatar.png", tokens.AccessToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/account/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profileResponse = await GetAuthAsync("/api/v1/account/profile", tokens.AccessToken);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<ApiResult<ProfileResponse>>())!;
        Assert.Null(profile.Data!.AvatarDataUri);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> UploadAvatarAsync(
        byte[] content, string contentType, string fileName, string token)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "File", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/avatar")
        {
            Content = multipart,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
