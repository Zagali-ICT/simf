// D-452 (Figma 883:2308 "تفاصيل اليوم") — programme-day admin CRUD + the
// one-active-day-per-date uniqueness guard, the Session.Type wiring, and the
// public day-grouped agenda (/app/programme/days). Mirrors SessionCategoriesTests.
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

[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ProgrammeDaysTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ProgrammeDaysTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_list_contains_the_day()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var date = new DateOnly(2030, 1, 5);

        var create = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = date,
                Title = "Opening Day",
                TitleArabic = "يوم الافتتاح",
                DisplayOrder = 1,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProgrammeDayDetail>>())!.Data!;
        Assert.Equal("Opening Day", created.Title);
        Assert.Equal(date, created.Date);
        Assert.True(created.IsActive);
        Assert.False(created.HasImage);

        var get = await GetAuthAsync($"/api/v1/admin/programme-days/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/programme-days/list", new GridQuery { Top = 200 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProgrammeDaySummary>>>())!.Data!;
        Assert.Contains(page.Items, d => d.Id == created.Id);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/programme-days/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_empty_title_is_400_PROGRAMME_DAY_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = new DateOnly(2030, 2, 1),
                Title = "   ",
                TitleArabic = "",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProgrammeDayInvalid, body.Error!.Code);
    }

    // Both siblings over this shape (AdminThemeService, AdminSponsorService) reject
    // a negative DisplayOrder on create AND update; the programme day accepted one,
    // which then sorts ahead of every legitimate row in the CP grid.
    [Fact]
    public async Task Create_with_a_negative_display_order_is_400_PROGRAMME_DAY_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = new DateOnly(2030, 3, 3),
                Title = "Negative order",
                TitleArabic = "ترتيب سالب",
                DisplayOrder = -1,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProgrammeDayInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Update_with_a_negative_display_order_is_400_PROGRAMME_DAY_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = new DateOnly(2030, 3, 4),
                Title = "Reorder me",
                TitleArabic = "أعد ترتيبي",
                DisplayOrder = 2,
            },
            token);
        var id = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProgrammeDayDetail>>())!.Data!.Id;

        var response = await PutAuthAsync(
            $"/api/v1/admin/programme-days/{id}",
            new AdminUpdateProgrammeDayRequest
            {
                Date = new DateOnly(2030, 3, 4),
                Title = "Reorder me",
                TitleArabic = "أعد ترتيبي",
                DisplayOrder = -5,
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProgrammeDayInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Second_active_day_on_the_same_date_is_400_PROGRAMME_DAY_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var date = new DateOnly(2031, 3, 3);

        var first = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = date, Title = "Day A", TitleArabic = "اليوم أ",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var clash = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = date, Title = "Day B", TitleArabic = "اليوم ب",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        var body = (await clash.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProgrammeDayInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Deactivate_marks_the_day_inactive()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = new DateOnly(2032, 4, 4), Title = "To remove", TitleArabic = "للحذف",
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProgrammeDayDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/programme-days/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/programme-days/{created.Id}", token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProgrammeDayDetail>>())!.Data!;
        Assert.False(fetched.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = new DateOnly(2033, 5, 5), Title = "Nope", TitleArabic = "لا",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Session_create_with_a_type_echoes_it_on_the_detail()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();
        var speakerId = await SeedSpeakerAsync();

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "TY-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Typed session", TitleArabic = "جلسة مصنّفة بالنوع",
                HallId = hallId,
                Type = SessionType.Workshop,
                // #4 — a Workshop is not an Event, so it needs a speaker.
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speakerId, "", "", 0),
                },
                Start = SimfClock.Now.AddHours(1),
                End = SimfClock.Now.AddHours(2),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionType.Workshop, detail.Type);
    }

    [Fact]
    public async Task Public_days_endpoint_groups_sessions_under_the_authored_day()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();
        var date = new DateOnly(2034, 6, 6);

        var dayCreate = await PostAuthAsync(
            "/api/v1/admin/programme-days",
            new AdminCreateProgrammeDayRequest
            {
                Date = date, Title = "Grouped Day", TitleArabic = "يوم مجمّع", DisplayOrder = 7,
            },
            token);
        var day = (await dayCreate.Content
            .ReadFromJsonAsync<ApiResult<AdminProgrammeDayDetail>>())!.Data!;

        // 08:00 a zoned value on the date → 11:00 KSA (+3), still the same calendar day,
        // so the session buckets onto the authored day.
        var speakerId = await SeedSpeakerAsync();
        var start = new DateTime(date.Year, date.Month, date.Day, 8, 0, 0);
        var sessionCreate = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "GD-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Grouped session", TitleArabic = "جلسة مجمّعة",
                HallId = hallId,
                Type = SessionType.Session,
                // #4 — a Session is not an Event, so it needs a speaker.
                Speakers = new List<AdminSessionSpeakerEntry>
                {
                    new(speakerId, "", "", 0),
                },
                Start = start,
                End = start.AddHours(1),
            },
            token);
        var session = (await sessionCreate.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;

        var publicDays = await _client.GetAsync("/api/v1/app/programme/days");
        Assert.Equal(HttpStatusCode.OK, publicDays.StatusCode);
        var payload = (await publicDays.Content
            .ReadFromJsonAsync<ApiResult<PublicProgrammeDays>>())!.Data!;

        var authored = Assert.Single(payload.Days, d => d.Id == day.Id);
        Assert.Equal("Grouped Day", authored.Title);
        Assert.Contains(authored.Sessions, s => s.Id == session.Id && s.Type == SessionType.Session);
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
            Name = "Hall PD", NameArabic = "قاعة",
            Capacity = 100,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    // #4 — the non-Event sessions in these tests need a speaker to be valid.
    private async Task<Guid> SeedSpeakerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "PD Speaker", NameArabic = "متحدّث",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"progday-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Programme Day Admin",
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
