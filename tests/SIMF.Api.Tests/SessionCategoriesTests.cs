// B9b — D-226: dynamic SessionCategory lookup admin CRUD + the
// Session.CategoryId FK wiring. Mirrors OrganisationTests / AdminSessionsTests.
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

public sealed class SessionCategoriesTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionCategoriesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_list_contains_the_category()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest
            {
                Name = "Main Session",
                NameArabic = "الجلسة الرئيسية",
                DisplayOrder = 1,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!;
        Assert.Equal("Main Session", created.Name);
        Assert.True(created.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/session-categories/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/session-categories/list", new GridQuery { Top = 200 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionCategorySummary>>>())!.Data!;
        Assert.Contains(page.Items, c => c.Id == created.Id);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/session-categories/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_empty_name_is_400_SESSION_CATEGORY_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = "   ", NameArabic = "" },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCategoryInvalid, body.Error!.Code);
    }

    // The unique index on SessionCategories.Name is FILTERED to the active rows
    // ("[IsActive] = 1"), so every path that leaves a second ACTIVE row holding one
    // name must be pre-checked. Without the pre-check the collision reaches
    // SaveChanges as a raw DbUpdateException and the caller sees a 500, not a 409.
    [Fact]
    public async Task Create_reusing_an_active_category_name_is_409_not_500()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var name = $"Plenary {Guid.NewGuid():N}"[..24];

        var first = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = name, NameArabic = "عام" },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = name, NameArabic = "عام آخر" },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCategoryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Renaming_a_category_onto_an_active_name_is_409_not_500()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var taken = $"Workshop {Guid.NewGuid():N}"[..24];

        await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = taken, NameArabic = "ورشة" },
            token);

        var other = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest
            {
                Name = $"Panel {Guid.NewGuid():N}"[..24],
                NameArabic = "حلقة نقاش",
            },
            token);
        var otherId = (await other.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!.Id;

        var rename = await PutAuthAsync(
            $"/api/v1/admin/session-categories/{otherId}",
            new AdminUpdateSessionCategoryRequest
            {
                Name = taken,
                NameArabic = "حلقة نقاش",
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
    }

    // The filtered index makes REACTIVATION a collision path of its own: the name
    // is unchanged, so a "check only when the name changed" guard (the sibling
    // pattern for an unfiltered unique key) would sail straight past it.
    [Fact]
    public async Task Reactivating_a_category_whose_name_is_now_taken_is_409_not_500()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var name = $"Forum {Guid.NewGuid():N}"[..24];

        var original = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = name, NameArabic = "منتدى" },
            token);
        var originalId = (await original.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!.Id;

        // Retired, so the filtered index no longer holds the name…
        await DeleteAuthAsync($"/api/v1/admin/session-categories/{originalId}", token);

        // …and a fresh active category legitimately claims it.
        var replacement = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = name, NameArabic = "منتدى جديد" },
            token);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);

        // Bringing the retired one back would put two ACTIVE rows on one name.
        var revive = await PutAuthAsync(
            $"/api/v1/admin/session-categories/{originalId}",
            new AdminUpdateSessionCategoryRequest
            {
                Name = name,
                NameArabic = "منتدى",
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.Conflict, revive.StatusCode);
    }

    [Fact]
    public async Task Deactivate_marks_the_category_inactive()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = "To remove", NameArabic = "للحذف" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/session-categories/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/session-categories/{created.Id}", token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!;
        Assert.False(fetched.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = "Nope", NameArabic = "لا" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Session_create_with_a_valid_category_echoes_it_and_unknown_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();

        var categoryCreate = await PostAuthAsync(
            "/api/v1/admin/session-categories",
            new AdminCreateSessionCategoryRequest { Name = "Keynote", NameArabic = "كلمة رئيسية" },
            token);
        var categoryId = (await categoryCreate.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionCategoryDetail>>())!.Data!.Id;

        // Valid category → echoed on the session detail.
        var ok = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "SC-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Categorised session", TitleArabic = "جلسة مصنّفة",
                HallId = hallId,
                CategoryId = categoryId,
                Type = SessionType.Event,
                Start = SimfClock.Now.AddHours(1),
                End = SimfClock.Now.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var detail = (await ok.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(categoryId, detail.CategoryId);

        // Unknown category → 400.
        var bad = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "SC-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Bad category", TitleArabic = "تصنيف خاطئ",
                HallId = hallId,
                CategoryId = Guid.NewGuid(),
                Start = SimfClock.Now.AddHours(1),
                End = SimfClock.Now.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = (await bad.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionCategoryInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall SC", NameArabic = "قاعة",
            Capacity = 100,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sescat-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Session Category Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
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
