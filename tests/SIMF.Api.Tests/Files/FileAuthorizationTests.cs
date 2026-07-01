// D-568 — the per-service download authorization matrix and the IDOR guarantee:
// the download endpoint authorizes off the stored file's own FileService policy
// (never the URL), so a guessed/leaked GUID for a private file is rejected with a
// uniform 404. Also covers the retention hold (IdDocument is not deletable).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests.Files;

public sealed class FileAuthorizationTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly byte[] Pdf = "%PDF-1.4\n%a presentation file for the authenticated test\n"u8.ToArray();

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public FileAuthorizationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task An_avatar_is_downloadable_only_by_its_owner_or_an_admin()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var (ownerId, ownerToken) = await CreateVisitorAsync();
        var stranger = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        // Admin stores an avatar owned by the visitor (owner id == the visitor's
        // identity, which is also their JWT `sub`).
        var url = await UploadAndGetUrlAsync(
            FileService.Avatar, FileOwnerEntityType.UserProfile, ownerId, Png, "image/png", "a.png", adminToken);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(url, ownerToken)).StatusCode);          // owner
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(url, adminToken)).StatusCode);          // admin (wildcard)
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(url, stranger.AccessToken)).StatusCode); // other user
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);        // anonymous
    }

    [Fact]
    public async Task A_vip_photo_is_admin_only()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var url = await UploadAndGetUrlAsync(
            FileService.VipPhoto, FileOwnerEntityType.UserProfile, Guid.NewGuid(), Png, "image/png", "v.png", adminToken);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(url, adminToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(url, visitor.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task A_speaker_presentation_is_readable_by_any_signed_in_account_but_not_anonymously()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var url = await UploadAndGetUrlAsync(
            FileService.SpeakerPresentation, FileOwnerEntityType.SpeakerPresentation, null, Pdf, "application/pdf", "deck.pdf", adminToken);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(url, visitor.AccessToken)).StatusCode); // any authenticated
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(url, adminToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);         // anonymous
    }

    [Fact]
    public async Task An_id_document_is_under_a_retention_hold_and_cannot_be_deleted()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();
        var resp = await UploadAsync(
            FileService.IdDocument, FileOwnerEntityType.UserProfile, owner, Png, "image/png", "id.png", adminToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var file = (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!;

        var del = await DeleteAsync($"/api/v1/files/{file.Id}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        // And it is still downloadable by the admin afterwards (not soft-deleted).
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(file.Url, adminToken)).StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> UploadAndGetUrlAsync(
        FileService service, FileOwnerEntityType ownerType, Guid? ownerId,
        byte[] bytes, string contentType, string fileName, string token)
    {
        var resp = await UploadAsync(service, ownerType, ownerId, bytes, contentType, fileName, token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ApiResult<UploadedFileResponse>>())!.Data!.Url;
    }

    private Task<HttpResponseMessage> UploadAsync(
        FileService service, FileOwnerEntityType ownerType, Guid? ownerId,
        byte[] bytes, string contentType, string fileName, string token)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(service.ToString()), "Service" },
            { new StringContent(ownerType.ToString()), "OwnerEntityType" },
        };
        if (ownerId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "OwnerEntityId");
        }
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    /// <summary>Registers + signs in a fresh visitor (2FA off) and returns their
    /// identity id (== the JWT <c>sub</c> the owner-check compares) and access token.</summary>
    private async Task<(Guid UserId, string Token)> CreateVisitorAsync()
    {
        var email = $"file-owner-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.DisableTwoFactor(_factory, email);

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;

        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            id = db.Users.Single(u => u.Email == email).Id;
        }
        return (id, token);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"file-authz-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "File Authz Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }
}
