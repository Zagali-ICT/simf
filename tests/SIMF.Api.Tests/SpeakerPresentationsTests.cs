// P2.3 — D-228 (FR-407, SIMF-FDS-004 §5.3): speaker presentation-file upload /
// list / download / delete over the admin Speakers.* surface. Bytes are stored
// out-of-row; the row links a speaker to a session.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class SpeakerPresentationsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakerPresentationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_then_list_then_download_round_trips_the_file()
    {
        var (speakerId, sessionId) = await SeedSpeakerAndSessionAsync();
        var admin = await CreateAdministratorAndSignInAsync();
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"

        var uploaded = await UploadAsync(speakerId, sessionId, "deck.pdf", "application/pdf", payload, admin);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var row = (await uploaded.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerPresentationRow>>())!.Data!;
        Assert.Equal("deck.pdf", row.FileName);
        Assert.Equal(payload.Length, row.SizeBytes);

        var list = await GetAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/presentations", admin);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<List<AdminSpeakerPresentationRow>>>())!.Data!;
        Assert.Contains(rows, r => r.Id == row.Id);

        var download = await GetAuthAsync(
            $"/api/v1/admin/speaker-presentations/{row.Id}/file", admin);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(payload, bytes);
    }

    [Fact]
    public async Task Delete_removes_the_presentation_from_the_list()
    {
        var (speakerId, sessionId) = await SeedSpeakerAndSessionAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var uploaded = await UploadAsync(
            speakerId, sessionId, "slides.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            new byte[] { 0x50, 0x4B, 0x03, 0x04 }, admin); // ZIP/OOXML magic
        var row = (await uploaded.Content
            .ReadFromJsonAsync<ApiResult<AdminSpeakerPresentationRow>>())!.Data!;

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/speaker-presentations/{row.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var list = await GetAuthAsync(
            $"/api/v1/admin/speakers/{speakerId}/presentations", admin);
        var rows = (await list.Content
            .ReadFromJsonAsync<ApiResult<List<AdminSpeakerPresentationRow>>>())!.Data!;
        Assert.DoesNotContain(rows, r => r.Id == row.Id);
    }

    [Fact]
    public async Task Upload_for_an_unknown_session_is_404()
    {
        var (speakerId, _) = await SeedSpeakerAndSessionAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        var uploaded = await UploadAsync(
            speakerId, Guid.NewGuid(), "deck.pdf", "application/pdf",
            new byte[] { 0x25, 0x50, 0x44, 0x46 }, admin); // "%PDF"
        Assert.Equal(HttpStatusCode.NotFound, uploaded.StatusCode);
        var body = (await uploaded.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_cannot_upload_a_presentation()
    {
        var (speakerId, sessionId) = await SeedSpeakerAndSessionAsync();
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var uploaded = await UploadAsync(
            speakerId, sessionId, "deck.pdf", "application/pdf",
            new byte[] { 1 }, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, uploaded.StatusCode);
    }

    [Fact]
    public async Task Upload_with_a_mismatched_or_disallowed_type_is_400()
    {
        var (speakerId, sessionId) = await SeedSpeakerAndSessionAsync();
        var admin = await CreateAdministratorAndSignInAsync();

        // M2 — a .pdf name but the bytes are not a PDF (nor any allowed
        // container) is rejected by the magic-byte allow-list.
        var uploaded = await UploadAsync(
            speakerId, sessionId, "evil.pdf", "application/pdf",
            "not-a-pdf"u8.ToArray(), admin);
        Assert.Equal(HttpStatusCode.BadRequest, uploaded.StatusCode);
        var body = (await uploaded.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SpeakerPresentationInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(Guid SpeakerId, Guid SessionId)> SeedSpeakerAndSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 50, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Dr. Speaker", NameArabic = "د. متحدث",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Speakers.Add(speaker);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote", TitleArabic = "كلمة",
            HallId = hall.Id,
            Start = DateTimeOffset.UtcNow.AddHours(1),
            End = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (speaker.Id, session.Id);
    }

    private Task<HttpResponseMessage> UploadAsync(
        Guid speakerId, Guid sessionId, string fileName, string contentType,
        byte[] bytes, string token)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/speakers/{speakerId}/presentations?sessionId={sessionId}")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sp-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "SP Admin",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
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
