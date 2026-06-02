// D-199 (Mockup screen 24) — public Archive endpoint: anonymous read + the
// archive-visibility gate (D-166). When the toggle is off the public list is
// empty regardless of how many active editions exist.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ArchiveTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ArchiveTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_list_is_anonymous_and_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/app/archive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<PublicArchive>>())!;
        Assert.True(body.Success);
        Assert.NotNull(body.Data!.Items);
    }

    [Fact]
    public async Task Public_list_shows_active_edition_when_visible()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        // Ensure the archive visibility toggle is on.
        await PutAuthAsync("/api/v1/admin/archive/visibility",
            new UpdateArchiveVisibilityRequest { IsVisible = true }, admin);

        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2018,
                TitleEn = "SIMF 2018",
                TitleAr = "سيمف 2018",
                SummaryEn = "The first edition",
                Attendees = 800,
                Sessions = 30,
                Speakers = 40,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await _client.GetAsync("/api/v1/app/archive");
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<PublicArchive>>())!.Data!;
        Assert.Contains(body.Items, e => e.Year == 2018 && e.TitleEn == "SIMF 2018");
    }

    [Fact]
    public async Task Public_list_is_empty_when_archive_visibility_is_off()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        // Seed one active edition so the only reason the public list could be
        // empty is the visibility gate.
        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2019,
                TitleEn = "SIMF 2019",
                TitleAr = "سيمف 2019",
                Attendees = 100,
                Sessions = 10,
                Speakers = 12,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        // Turn the archive visibility toggle off.
        var off = await PutAuthAsync("/api/v1/admin/archive/visibility",
            new UpdateArchiveVisibilityRequest { IsVisible = false }, admin);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        try
        {
            var list = await _client.GetAsync("/api/v1/app/archive");
            var body = (await list.Content
                .ReadFromJsonAsync<ApiResult<PublicArchive>>())!.Data!;
            Assert.Empty(body.Items);
        }
        finally
        {
            // Restore the default (visible) so other tests are unaffected.
            await PutAuthAsync("/api/v1/admin/archive/visibility",
                new UpdateArchiveVisibilityRequest { IsVisible = true }, admin);
        }
    }

    // -- helpers --

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"archive-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Archive Admin",
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
}
