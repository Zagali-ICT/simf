// Tests: D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows + free slots.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class HallAvailabilityTests : IClassFixture<SimfApiFactory>
{
    // Far-future windows so the slots are in the future regardless of the test clock.
    private static readonly DateTimeOffset WindowStart = new(2030, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public HallAvailabilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_window_then_it_lists_and_yields_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();

        // A 60-minute window with 30-minute slots → 2 slots.
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                StartUtc = WindowStart, EndUtc = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var windows = await GetWindowsAsync(hallId, admin);
        Assert.Single(windows);

        var slots = await GetSlotsAsync(hallId, admin);
        Assert.Equal(2, slots.Count);
        Assert.Equal(WindowStart, slots[0].StartUtc);
        Assert.Equal(WindowStart.AddMinutes(30), slots[0].EndUtc);
    }

    [Fact]
    public async Task An_invalid_window_is_400_and_an_unknown_hall_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();

        // End before start → 400.
        var bad = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                StartUtc = WindowStart, EndUtc = WindowStart.AddMinutes(-10), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Unknown hall → 404.
        var unknown = await PostAuthAsync(
            $"/api/v1/admin/halls/{Guid.NewGuid()}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                StartUtc = WindowStart, EndUtc = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Delete_window_removes_its_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                StartUtc = WindowStart, EndUtc = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        var window = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminHallAvailabilityWindow>>())!.Data!;

        var del = await SendAuthAsync(HttpMethod.Delete,
            $"/api/v1/admin/hall-availability-windows/{window.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        Assert.Empty(await GetWindowsAsync(hallId, admin));
        Assert.Empty(await GetSlotsAsync(hallId, admin));
    }

    // -- helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<AdminHallAvailabilityWindow>> GetWindowsAsync(
        Guid hallId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/halls/{hallId}/availability-windows", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminHallAvailabilityWindow>>>())!.Data!;
    }

    private async Task<IReadOnlyList<HallAvailableSlot>> GetSlotsAsync(Guid hallId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/halls/{hallId}/available-slots", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<HallAvailableSlot>>>())!.Data!;
    }

    private async Task<Guid> SeedHallAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall", NameArabic = "قاعة الاجتماعات",
            Purpose = HallPurpose.Meeting,
            Capacity = 20, IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"hall-avail-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Hall Avail Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
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

    private Task<HttpResponseMessage> SendAuthAsync(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
