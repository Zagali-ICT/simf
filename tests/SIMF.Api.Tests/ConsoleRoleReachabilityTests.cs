// The two operator consoles load for the role that runs them.
//
// CpPageEndpointReachabilityTests proves this statically over the checked-in
// markup: no CP page calls an endpoint its own gate cannot reach. These are the
// behavioural half - a real HTTP call, from a real account holding ONLY the
// console's permission, against the running host.
//
// Both are worth having. The static guard sees every page and fails at build
// time; it cannot see a runtime policy, a filter that hides rows, or a route
// that never got mapped through the BFF. These two see exactly one call each,
// end to end, and would still catch the defect if the static parse ever drifted.
//
// The defect they pin: /admin/sessions/live-hall (gated Attendance.View) and
// /admin/hall-arrivals (gated HallArrivals.View) both filled their session
// picker from /admin/sessions/list, which is gated Sessions.View. SecurityTeam -
// the baseline role for both consoles, and the role standing at a hall door -
// holds neither Sessions.View nor anything that implies it. So each console
// opened for its own operator and its first fetch 403'd: an empty picker, a
// toast, and no way forward.
//
// It survived every existing test because they all sign in as the seeded
// super-administrator, whose "*" wildcard satisfies any gate. A fixture that
// grants exactly the permission under test is the only thing that can see it,
// which is the whole point of building the account by hand below.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Attendance;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ConsoleRoleReachabilityTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ConsoleRoleReachabilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    /// <summary>The live-hall monitor's picker, for a holder of nothing but
    /// <c>Attendance.View</c>.</summary>
    [Fact]
    public async Task Attendance_view_alone_can_fill_the_live_hall_session_picker()
    {
        var token = await CreateAdminWithPermissionsAsync(PermissionCatalog.Attendance.View);

        var response = await PostAsync(
            "/api/v1/admin/attendance/sessions/list", new GridQuery { Top = 200 }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<SessionAttendanceRow>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
    }

    /// <summary>The hall-arrival console's picker, for a holder of nothing but
    /// <c>HallArrivals.View</c>.</summary>
    [Fact]
    public async Task Hall_arrivals_view_alone_can_fill_the_arrival_session_picker()
    {
        var token = await CreateAdminWithPermissionsAsync(PermissionCatalog.HallArrivals.View);

        var response = await PostAsync(
            "/api/v1/admin/hall-arrivals/sessions/list", new GridQuery { Top = 200 }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<HallArrivalSessionOption>>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
    }

    /// <summary>The canonical sessions list stays shut to both consoles' roles.
    ///
    /// <para>Without this the fix could be "read" as having widened
    /// <c>/admin/sessions/list</c>, which would have been the wrong repair: that
    /// route is shared by the whole Sessions module, and loosening it to unblock
    /// one picker would hand every console role the session catalogue. This
    /// asserts the console was moved to its own read rather than the shared read
    /// being opened up.</para></summary>
    [Fact]
    public async Task Neither_console_role_gains_the_canonical_sessions_list()
    {
        foreach (var permission in new[]
                 {
                     PermissionCatalog.Attendance.View,
                     PermissionCatalog.HallArrivals.View,
                 })
        {
            var token = await CreateAdminWithPermissionsAsync(permission);

            var response = await PostAsync(
                "/api/v1/admin/sessions/list", new GridQuery { Top = 200 }, token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>Viewing a console must not carry the right to write through it.
    ///
    /// <para>The arrival picker was given <c>HallArrivals.View</c> rather than
    /// <c>.Record</c> deliberately: an operator who may see the console may fill
    /// its picker. That is only sound while recording still demands
    /// <c>.Record</c>, so this pins the half that keeps the split honest.</para>
    /// </summary>
    [Fact]
    public async Task Hall_arrivals_view_alone_cannot_record_an_arrival()
    {
        var token = await CreateAdminWithPermissionsAsync(PermissionCatalog.HallArrivals.View);

        var response = await PostAsync(
            $"/api/v1/admin/sessions/{Guid.NewGuid()}/arrivals",
            new RecordQrArrivalRequest { QrId = "NOTAREALBADGE" },
            token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The arrival picker is scoped to ACTIVE sessions server-side.
    ///
    /// <para>The console used to filter <c>IsActive</c> off its own rows, and a
    /// bUnit test pinned that. The console no longer receives the field, so the
    /// guarantee moved into the query - ahead of the grid filters, where no
    /// request can widen it - and the test moved here with it. Seeds one active
    /// and one inactive session in the same hall and asserts the endpoint returns
    /// exactly one of them.</para></summary>
    [Fact]
    public async Task The_arrival_picker_is_scoped_to_active_sessions()
    {
        var (activeId, inactiveId) = await SeedActiveAndInactiveSessionAsync();
        var token = await CreateAdminWithPermissionsAsync(PermissionCatalog.HallArrivals.View);

        var response = await PostAsync(
            "/api/v1/admin/hall-arrivals/sessions/list",
            new GridQuery { Top = 200 }, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<HallArrivalSessionOption>>>();
        Assert.NotNull(body);
        var ids = body!.Data!.Items.Select(row => row.Id).ToList();

        Assert.Contains(activeId, ids);
        Assert.DoesNotContain(inactiveId, ids);
    }

    /// <summary>One active and one inactive session in a fresh hall. Both are
    /// inside the arrival window, so the only thing separating them is
    /// <c>IsActive</c> - otherwise a window filter could pass this test while the
    /// scope was missing.</summary>
    private async Task<(Guid Active, Guid Inactive)> SeedActiveAndInactiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Console Hall",
            NameArabic = "قاعة الكونسول",
            Capacity = 100,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        Session Make(bool isActive) => new()
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = isActive ? "Console Active" : "Console Inactive",
            TitleArabic = "جلسة",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(-15),
            End = SimfClock.Now.AddMinutes(45),
            IsActive = isActive,
            CreatedAt = SimfClock.Now,
        };

        var active = Make(isActive: true);
        var inactive = Make(isActive: false);
        db.Sessions.AddRange(active, inactive);
        await db.SaveChangesAsync();
        return (active.Id, inactive.Id);
    }

    /// <summary>An approved admin account in a fresh role granted exactly the
    /// named permissions and nothing else. Not a baseline role: SecurityTeam
    /// carries eight permissions, and a test that granted all eight could not say
    /// which one carried the call.</summary>
    private async Task<string> CreateAdminWithPermissionsAsync(params string[] codes)
    {
        var email = $"console-role-{Guid.NewGuid():N}@simf.test";
        var roleName = $"ConsoleOnly-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roles.CreateAsync(role);

            foreach (var code in codes)
            {
                // Fails loudly on a code that is not in the catalogue, so a
                // renamed permission breaks this test rather than silently
                // granting nothing and leaving it green for the wrong reason.
                var definition = PermissionCatalog.All.Single(p => p.Code == code);
                var permission = await db.Permissions
                    .SingleOrDefaultAsync(p => p.Code == definition.Code);
                if (permission is null)
                {
                    permission = new Permission { Id = Guid.NewGuid(), Code = definition.Code };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
            await db.SaveChangesAsync();

            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Console Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
