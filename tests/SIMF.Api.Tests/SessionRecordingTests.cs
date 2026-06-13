// P3.2b — D-232 (D-213): session recording storage + range-streaming behind a
// short-lived, recording-scoped stream token. Covers upload/delete, the
// publish+recording visibility gate, token minting, full + range streaming,
// the cross-session token guard, and the no-token + non-admin rejections.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SessionRecordingTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private static readonly byte[] SampleRecording =
        "FAKE-MP4-RECORDING-BYTES-0123456789"u8.ToArray();

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionRecordingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_attaches_the_recording_and_sets_metadata()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var detail = await UploadRecordingAsync(session.Id, token);
        Assert.True(detail.HasRecording);
        Assert.Equal("recording.mp4", detail.RecordingFileName);
        Assert.Equal(SampleRecording.Length, detail.RecordingSizeBytes);
        Assert.NotNull(detail.RecordingUploadedAt);
    }

    [Fact]
    public async Task Public_detail_hides_the_recording_until_published()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        await UploadRecordingAsync(session.Id, token);

        // Recording attached but the session is still Scheduled — not visible.
        var before = await ReadPublicDetailAsync(session.Id);
        Assert.False(before.HasRecording);

        await PublishAsync(session.Id, token);

        var after = await ReadPublicDetailAsync(session.Id);
        Assert.True(after.HasRecording);
    }

    [Fact]
    public async Task Token_endpoint_returns_a_token_for_a_published_recording()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateAndPublishWithRecordingAsync(token);

        var response = await PostAuthAsync(
            $"/api/v1/app/programme/sessions/{session}/recording/token",
            new { }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecordingStreamTokenResponse>>())!.Data!;
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        // The minted URL must be the real, registered stream route (incl. the
        // /app segment from the D-247 route split) — a client uses it verbatim.
        Assert.Equal(
            $"/api/v1/app/programme/sessions/{session}/recording/stream",
            body.StreamUrl);
        Assert.True(body.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task Token_endpoint_is_404_when_the_session_is_not_published()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        await UploadRecordingAsync(session.Id, token); // recorded but not published

        var response = await PostAuthAsync(
            $"/api/v1/app/programme/sessions/{session.Id}/recording/token",
            new { }, token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionRecordingNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Stream_returns_the_recorded_bytes()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateAndPublishWithRecordingAsync(token);
        var streamToken = await MintStreamTokenAsync(session, token);

        var response = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{session}/recording/stream?access_token={streamToken}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(SampleRecording, bytes);
        // Defence in depth: the browser must not MIME-sniff the streamed bytes.
        Assert.Contains("nosniff", response.Headers.TryGetValues("X-Content-Type-Options", out var v)
            ? string.Join(",", v) : string.Empty);
    }

    [Fact]
    public async Task Upload_of_a_non_video_file_is_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/sessions/{session.Id}/recording")
        {
            Content = BuildRecordingForm("malicious.html", "text/html"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionRecordingInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Stream_supports_http_range_requests()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateAndPublishWithRecordingAsync(token);
        var streamToken = await MintStreamTokenAsync(session, token);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/app/programme/sessions/{session}/recording/stream?access_token={streamToken}");
        request.Headers.Range = new RangeHeaderValue(0, 3);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(SampleRecording[..4], bytes);
    }

    [Fact]
    public async Task Stream_with_a_token_scoped_to_another_session_is_403()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var sessionA = await CreateAndPublishWithRecordingAsync(token);
        var sessionB = await CreateAndPublishWithRecordingAsync(token);

        // A token minted for B must not stream A.
        var tokenForB = await MintStreamTokenAsync(sessionB, token);
        var response = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{sessionA}/recording/stream?access_token={tokenForB}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stream_without_a_token_is_401()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateAndPublishWithRecordingAsync(token);

        var response = await _client.GetAsync(
            $"/api/v1/app/programme/sessions/{session}/recording/stream");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_recording()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        await UploadRecordingAsync(session.Id, token);

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/sessions/{session.Id}/recording", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var detail = (await delete.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.False(detail.HasRecording);
        Assert.Null(detail.RecordingFileName);
    }

    [Fact]
    public async Task Non_admin_caller_cannot_upload_a_recording()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(adminToken);

        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var multipart = BuildRecordingForm();
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/sessions/{session.Id}/recording")
        {
            Content = multipart,
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateAndPublishWithRecordingAsync(string token)
    {
        var session = await CreateSessionAsync(token);
        await UploadRecordingAsync(session.Id, token);
        await PublishAsync(session.Id, token);
        return session.Id;
    }

    private async Task PublishAsync(Guid id, string token)
    {
        await SetStatusAsync(id, SessionStatus.Held, token);
        await SetStatusAsync(id, SessionStatus.Recorded, token);
        await SetStatusAsync(id, SessionStatus.Published, token);
    }

    private Task<HttpResponseMessage> SetStatusAsync(Guid id, SessionStatus status, string token) =>
        PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = status }, token);

    private async Task<string> MintStreamTokenAsync(Guid id, string token)
    {
        var response = await PostAuthAsync(
            $"/api/v1/app/programme/sessions/{id}/recording/token", new { }, token);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<RecordingStreamTokenResponse>>())!.Data!;
        return body.Token;
    }

    private async Task<AdminSessionDetail> UploadRecordingAsync(Guid id, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/sessions/{id}/recording")
        {
            Content = BuildRecordingForm(),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private static MultipartFormDataContent BuildRecordingForm(
        string fileName = "recording.mp4", string contentType = "video/mp4")
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(SampleRecording);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private async Task<PublicSessionDetail> ReadPublicDetailAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/v1/app/programme/sessions/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
    }

    private async Task<AdminSessionDetail> CreateSessionAsync(string token)
    {
        var hall = await SeedHallAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "REC-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Recording session",
                TitleArabic = "جلسة التسجيل",
                HallId = hall.Id,
                StartUtc = DateTimeOffset.UtcNow.AddHours(1),
                EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    private async Task<Hall> SeedHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Recording Hall",
            NameArabic = "قاعة التسجيل",
            Capacity = 100,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"rec-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Recording Admin",
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
