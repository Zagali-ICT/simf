// What may be recorded as an external link, and what may not.
//
// Two rules, and they pull in opposite directions, which is why they are pinned
// together rather than in the file for whichever feature added them:
//
//   * a PRIVATE file may never become a link at all. An external link stores no
//     bytes, so it skips the malware scan, the magic-byte check and encryption
//     at rest. For an ID document or an avatar that is not a shortcut, it is a
//     way to serve somebody else's server under this system's name.
//
//   * an IMAGE link must NOT be held to the video rule. The obvious-looking
//     hardening — "validate every external link with LiveStreamUrlPolicy" —
//     accepts only a YouTube id or a .m3u8/.mp4 suffix, and would reject every
//     CDN logo, the seeded placeholder logos, and the whole "External link" tab
//     of the shared upload control. The image surfaces never read the URL: the
//     download endpoint 302s and the client follows it.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ExternalLinkPolicyTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExternalLinkPolicyTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Theory]
    // Extensionless, query-string placeholder — exactly the shape the seeded
    // partner logos use, and the shape a naive video rule would reject.
    [InlineData("https://placehold.co/260x130/ffffff/0a2e6b?text=Maritime%20News")]
    [InlineData("https://cdn.example.com/logo.png")]
    [InlineData("https://cdn.example.com/brand/logo")]
    public async Task An_image_link_is_accepted_whatever_its_path_looks_like(string url)
    {
        var token = await CreateAdministratorAndSignInAsync();
        var owner = Guid.NewGuid();

        var resp = await PutAuthAsync(
            $"/api/v1/admin/assets/SponsorLogo/{owner}/link",
            new SetAssetLinkRequest { Kind = AssetKind.Image, Url = url },
            token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task A_private_file_service_refuses_an_external_link()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await PostAuthAsync(
            "/api/v1/files/link",
            new { Service = FileService.IdDocument, Url = "https://cdn.example.com/passport.jpg" },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task An_avatar_refuses_an_external_link_too()
    {
        // Avatar is Confidential and encrypted at rest; a link row would be
        // neither, while still being downloadable through the same endpoint.
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await PostAuthAsync(
            "/api/v1/files/link",
            new { Service = FileService.Avatar, Url = "https://cdn.example.com/face.jpg" },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://cdn.example.com/stream/hero.mp4")]
    [InlineData("https://cdn.example.com/stream/live.m3u8")]
    public async Task A_video_service_accepts_a_link_its_players_can_classify(string url)
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await PostAuthAsync(
            "/api/v1/files/link",
            new { Service = FileService.OrganizationHeroVideo, Url = url },
            token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Theory]
    // Public https, real host, and completely unplayable: no YouTube id and no
    // stream suffix. Stored, this produces a silent empty hero on one surface and
    // a player error on another — so it is refused at the door instead.
    [InlineData("https://cdn.example.com/brand/promo")]
    [InlineData("https://cdn.example.com/brand/promo.png")]
    [InlineData("https://www.youtube.com/@somechannel")]
    public async Task A_video_service_refuses_a_link_no_player_can_classify(string url)
    {
        var token = await CreateAdministratorAndSignInAsync();

        var resp = await PostAuthAsync(
            "/api/v1/files/link",
            new { Service = FileService.OrganizationHeroVideo, Url = url },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"link-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Link Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
