// A9 (PII) — the admin READ of a subject's national-ID document image is a PII
// disclosure and must leave an audit trail, mirroring the already-audited upload.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>A9 — every admin fetch of a visitor's ID-document image writes a
/// <c>UserProfile.IdImageViewed</c> audit row naming the acting admin + the
/// subject. Runs on the base factory (the C7 face gate is off), and seeds the
/// stored image in-process through <see cref="IUserProfileService"/> so the HTTP
/// GET exercises the real, audited read path.</summary>
public sealed class AdminIdDocumentAuditTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminIdDocumentAuditTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_fetch_of_a_visitor_id_image_streams_the_bytes_and_audits_the_read()
    {
        var (adminToken, adminId) = await CreateAdministratorAndSignInAsync();
        var (visitorId, visitorEmail) = await CreateVisitorWithProfileAsync();

        // Seed a stored ID image for the visitor via the real service (encrypted at
        // rest). The face gate is off on the base factory and the admin upload
        // service does not face-gate, so a small valid PNG is accepted.
        var png = ValidPng();
        using (var scope = _factory.Services.CreateScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
            await profiles.UploadIdImageForSubjectAsync(
                adminId, visitorId, UserType.Visitor, png, "image/png");
        }

        // The real, audited admin read path.
        var fetch = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitorId}/id-document", adminToken);
        Assert.Equal(HttpStatusCode.OK, fetch.StatusCode);
        Assert.NotEmpty(await fetch.Content.ReadAsByteArrayAsync());
        Assert.Equal("no-store", fetch.Headers.CacheControl?.ToString());

        // A Viewed audit row now names the acting admin + the subject.
        var viewed = FindAuditEntry(visitorEmail, AuditEvents.UserProfileIdImageViewed);
        Assert.NotNull(viewed);
        Assert.Equal(adminId, viewed!.ActorUserId);
        Assert.Equal(visitorId, viewed.SubjectUserId);
    }

    [Fact]
    public async Task Admin_fetch_when_no_image_is_on_file_is_404_and_writes_no_view_audit()
    {
        // A read that discloses no bytes (404) must NOT write a Viewed row — only
        // an actual PII disclosure is audited.
        var (adminToken, _) = await CreateAdministratorAndSignInAsync();
        var (visitorId, visitorEmail) = await CreateVisitorWithProfileAsync();

        var fetch = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitorId}/id-document", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, fetch.StatusCode);

        Assert.Null(FindAuditEntry(visitorEmail, AuditEvents.UserProfileIdImageViewed));
    }

    // -- Helpers --------------------------------------------------------------

    private OperationLogEntry? FindAuditEntry(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return db.OperationLog
            .FirstOrDefault(entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }

    private static byte[] ValidPng()
    {
        using var image = new SixLabors.ImageSharp.Image<
            SixLabors.ImageSharp.PixelFormats.Rgb24>(16, 16);
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
        return stream.ToArray();
    }

    private async Task<(string Token, Guid AdminId)> CreateAdministratorAndSignInAsync()
    {
        var email = $"iddoc-admin-{Guid.NewGuid():N}@simf.test";
        Guid adminId;
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
                DisplayName = "Id-Doc Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            adminId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, adminId);
    }

    private async Task<(Guid Id, string Email)> CreateVisitorWithProfileAsync()
    {
        var email = $"iddoc-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Id-Doc Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
        return (user.Id, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
