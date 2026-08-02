// H-3 — a hall Capacity reduction must not drop below what the hall already
// commits: its seat-layout total (rows × seats). An increase always passes; a
// hall with no layout / reservations may shrink freely.
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
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminHallCapacityTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminHallCapacityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Capacity_below_seat_layout_total_is_409()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin, capacity: 50);
        await SeedLayoutAsync(hall.Id, "A,B,C,D,E", seatsPerRow: 10); // 5 × 10 = 50

        var response = await UpdateCapacityAsync(admin, hall, newCapacity: 40);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallCapacityBelowUsage, body.Error!.Code);
    }

    [Fact]
    public async Task Capacity_at_committed_total_succeeds()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin, capacity: 60);
        await SeedLayoutAsync(hall.Id, "A,B,C,D,E", seatsPerRow: 10); // 50

        var response = await UpdateCapacityAsync(admin, hall, newCapacity: 50);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Capacity_increase_skips_the_back_check()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin, capacity: 50);
        await SeedLayoutAsync(hall.Id, "A,B,C,D,E", seatsPerRow: 10); // 50

        var response = await UpdateCapacityAsync(admin, hall, newCapacity: 80);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hall_with_no_layout_or_reservations_may_shrink_freely()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var hall = await CreateHallAsync(admin, capacity: 100);

        var response = await UpdateCapacityAsync(admin, hall, newCapacity: 1);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "HC" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<AdminHallDetail> CreateHallAsync(string token, int capacity)
    {
        var response = await PostAuthAsync("/api/v1/admin/halls", new AdminCreateHallRequest
        {
            Code = NewCode(), Name = "Capacity Hall", NameArabic = "قاعة السعة", Capacity = capacity,
        }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!.Data!;
    }

    private Task<HttpResponseMessage> UpdateCapacityAsync(
        string token, AdminHallDetail hall, int newCapacity) =>
        PutAuthAsync($"/api/v1/admin/halls/{hall.Id}", new AdminUpdateHallRequest
        {
            Code = hall.Code, Name = hall.Name, NameArabic = hall.NameArabic,
            Capacity = newCapacity, IsActive = true,
        }, token);

    private async Task SeedLayoutAsync(Guid hallId, string rowLabels, int seatsPerRow)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hallId,
            RowLabels = rowLabels,
            SeatsPerRow = seatsPerRow,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"hallcap-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Hall Cap Admin",
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
