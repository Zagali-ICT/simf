// D-199 — admin Archive edition CRUD: create/get/list roundtrip,
// duplicate-year 409, and auth gating.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminArchiveTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminArchiveTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_create_then_get_roundtrips()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // 2019 is deliberately OUTSIDE the seeded edition set (IdentitySeeder
        // seeds 2022–2025) so this create cannot collide with seeded demo data
        // and return 409 — that collision was the cause of the historical
        // AdminArchive flake. Keep this year out of the seeded range.
        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2019,
                TitleEn = "SIMF 2019",
                TitleAr = "سيمف 2019",
                SummaryEn = "A test edition for the create/get roundtrip.",
                SummaryAr = "نسخة اختبارية لمسار الإنشاء والاسترجاع.",
                Attendees = 1200,
                Sessions = 45,
                Speakers = 80,
                CoverImageRelativePath = "archive/simf2019.png",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(2019, created.Year);
        Assert.Equal(1200, created.Attendees);

        var get = await GetAuthAsync($"/api/v1/admin/archive/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;
        Assert.Equal("SIMF 2019", detail.TitleEn);
        Assert.Equal(80, detail.Speakers);
    }

    [Fact]
    public async Task Admin_create_and_list_contains_edition()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2021,
                TitleEn = "SIMF 2021",
                TitleAr = "سيمف 2021",
                Attendees = 900,
                Sessions = 35,
                Speakers = 60,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await PostAuthAsync("/api/v1/admin/archive/list",
            new GridQuery { Top = 500 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminArchiveEditionSummary>>>())!.Data!;
        Assert.Contains(body.Items, e => e.Year == 2021);
    }

    [Fact]
    public async Task Create_duplicate_year_returns_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var first = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2017,
                TitleEn = "SIMF 2017",
                TitleAr = "سيمف 2017",
                Attendees = 700,
                Sessions = 25,
                Speakers = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2017,
                TitleEn = "SIMF 2017 Duplicate",
                TitleAr = "سيمف 2017 مكرر",
                Attendees = 1,
                Sessions = 1,
                Speakers = 1,
            }, admin);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2016,
                TitleEn = "Forbidden",
                TitleAr = "ممنوع",
                Attendees = 1,
                Sessions = 1,
                Speakers = 1,
            }, visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // §9 (Mockup screen 24-01) — place + date label round-trip through CRUD.
    [Fact]
    public async Task Admin_create_roundtrips_location_and_date_label()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2013,
                TitleEn = "SIMF 2013",
                TitleAr = "سيمف 2013",
                Attendees = 300,
                Sessions = 12,
                Speakers = 15,
                LocationEn = "Jeddah · Corniche",
                LocationAr = "جدة · الكورنيش",
                DateLabelEn = "October 2013 · 2 days",
                DateLabelAr = "أكتوبر 2013 · يومان",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;
        Assert.Equal("Jeddah · Corniche", created.LocationEn);
        Assert.Equal("جدة · الكورنيش", created.LocationAr);
        Assert.Equal("October 2013 · 2 days", created.DateLabelEn);
        Assert.Equal("أكتوبر 2013 · يومان", created.DateLabelAr);

        var get = await GetAuthAsync($"/api/v1/admin/archive/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;
        Assert.Equal("Jeddah · Corniche", detail.LocationEn);
        Assert.Equal("أكتوبر 2013 · يومان", detail.DateLabelAr);
    }

    // D-275 (§9) — "make this year history" one-click snapshot.
    [Fact]
    public async Task Snapshot_creates_current_year_edition_and_duplicate_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var snap = await PostAuthAsync("/api/v1/admin/archive/snapshot-current",
            new SnapshotCurrentEditionRequest { MakeVisible = true }, admin);
        Assert.Equal(HttpStatusCode.OK, snap.StatusCode);
        var created = (await snap.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;

        // Year + bilingual title are generated; the three counters are computed.
        Assert.True(created.Year >= 2000);
        Assert.Equal($"SIMF {created.Year}", created.TitleEn);
        Assert.Equal($"سيمف {created.Year}", created.TitleAr);
        Assert.True(created.Attendees >= 0);
        Assert.True(created.Sessions >= 0);
        Assert.True(created.Speakers >= 0);

        // The snapshot is a real edition — present in the admin list (not gated).
        var list = await PostAuthAsync("/api/v1/admin/archive/list",
            new GridQuery { Top = 500 }, admin);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminArchiveEditionSummary>>>())!.Data!;
        Assert.Contains(page.Items, e => e.Year == created.Year);

        // One edition per year — a second snapshot of the same year is a 409.
        var again = await PostAuthAsync("/api/v1/admin/archive/snapshot-current",
            new SnapshotCurrentEditionRequest { MakeVisible = false }, admin);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Snapshot_is_forbidden_for_a_non_admin()
    {
        var visitor = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync("/api/v1/admin/archive/snapshot-current",
            new SnapshotCurrentEditionRequest { MakeVisible = false },
            visitor.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_detail_with_multiple_children_returns_each_list_intact()
    {
        // A6 — the admin detail read AsSplitQuery's its three SIBLING child
        // collections (Gallery + SessionTitles + PastSpeakers). Populating all
        // three with multiple rows proves the split returns each list at its
        // authored count and order; a single-query cartesian would multiply them
        // (3×2×2). No sibling test seeds children, so this is the first exercise
        // of that path. 2011 is outside the seeded range (2022–2025).
        var admin = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync("/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = 2011,
                TitleEn = "SIMF 2011",
                TitleAr = "سيمف 2011",
                Attendees = 100,
                Sessions = 10,
                Speakers = 12,
                Gallery = new List<ArchiveMediaItemInput>
                {
                    new() { Kind = 0, Url = "archive/2011/a.png", DisplayOrder = 0 },
                    new() { Kind = 0, Url = "archive/2011/b.png", DisplayOrder = 1 },
                    new() { Kind = 1, Url = "archive/2011/c.mp4", DisplayOrder = 2 },
                },
                SessionTitles = new List<ArchiveSessionTitleInput>
                {
                    new() { TitleEn = "Opening", TitleAr = "الافتتاح", DisplayOrder = 0 },
                    new() { TitleEn = "Closing", TitleAr = "الختام", DisplayOrder = 1 },
                },
                PastSpeakers = new List<ArchivePastSpeakerInput>
                {
                    new() { NameEn = "Alpha", NameAr = "ألفا", DisplayOrder = 0 },
                    new() { NameEn = "Beta", NameAr = "بيتا", DisplayOrder = 1 },
                },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;

        var get = await GetAuthAsync($"/api/v1/admin/archive/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminArchiveEditionDetail>>())!.Data!;

        // Each child list returns at its authored count (no cartesian multiplication)
        // and in ascending DisplayOrder (ToDetail re-sorts by DisplayOrder).
        Assert.Equal(
            new[] { "archive/2011/a.png", "archive/2011/b.png", "archive/2011/c.mp4" },
            detail.Gallery!.Select(g => g.Url).ToArray());
        Assert.Equal(
            new[] { "Opening", "Closing" },
            detail.SessionTitles!.Select(s => s.TitleEn).ToArray());
        Assert.Equal(
            new[] { "Alpha", "Beta" },
            detail.PastSpeakers!.Select(p => p.NameEn).ToArray());
    }

    // -- helpers --

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-archive-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Admin Archive",
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
}
