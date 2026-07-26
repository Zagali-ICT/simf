// Tests: QA B16 — GET /admin/halls/{hallId}/schedule (the hall occupancy view).
//
// The occupancy view exists to show, up front, the rule that used to surface
// only as a 409 from EnsureNoHallTimeOverlapAsync. That guard matches on
// `other.IsActive`, so the view must too: a soft-deleted session is NOT a
// booking and must not make a hall read as BUSY. The panel's Status column
// shows the SessionStatus lifecycle (Scheduled/Held/Recorded/Published), not
// IsActive, so a leaked deactivated row is indistinguishable from a live one.
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

public sealed class HallScheduleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    // Far-future so the rows never collide with other suites' seeded sessions.
    private static readonly DateTimeOffset ScheduleStart =
        new(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public HallScheduleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Schedule_lists_only_the_active_sessions_in_this_hall()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin);

        var live = await SeedSessionAsync(hall.Id, ScheduleStart, isActive: true);
        var deleted = await SeedSessionAsync(
            hall.Id, ScheduleStart.AddHours(2), isActive: false);

        var page = await GetScheduleAsync(hall.Id, admin);

        var codes = page.Items.Select(session => session.Code).ToList();
        Assert.Contains(live, codes);
        // The defect: without the isActive filter the soft-deleted row came back
        // and rendered as a live booking.
        Assert.DoesNotContain(deleted, codes);
        Assert.All(page.Items, session => Assert.True(session.IsActive));

        // Total is the filtered count, so the panel's "capped" check compares
        // like with like.
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task Schedule_of_a_hall_whose_only_session_is_deleted_is_empty()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin);
        await SeedSessionAsync(hall.Id, ScheduleStart, isActive: false);

        var page = await GetScheduleAsync(hall.Id, admin);

        // A hall the occupancy view calls BUSY must be a hall the overlap guard
        // would refuse. Deactivating the only session frees the hall, so the
        // view has to say so.
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<GridPage<AdminSessionSummary>> GetScheduleAsync(
        Guid hallId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/halls/{hallId}/schedule");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!;
        return body.Data!;
    }

    private static string NewCode() =>
        "HS" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<AdminHallDetail> CreateHallAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/halls")
        {
            Content = JsonContent.Create(new AdminCreateHallRequest
            {
                Code = NewCode(),
                Name = "Occupancy Hall",
                NameArabic = "قاعة الإشغال",
                Capacity = 120,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
    }

    // Seeded straight to the DB: the delete endpoint soft-deletes, but seeding
    // both states here keeps the test about the read, not the write path.
    private async Task<string> SeedSessionAsync(
        Guid hallId, DateTimeOffset start, bool isActive)
    {
        var code = NewCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = isActive ? "Live Booking" : "Cancelled Booking",
            TitleArabic = isActive ? "حجز قائم" : "حجز ملغى",
            HallId = hallId,
            Start = start,
            End = start.AddHours(1),
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return code;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"hall-schedule-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Hall Schedule Admin",
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
                Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }
}
