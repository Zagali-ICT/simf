// Tests: D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows + free slots.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Seats)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class HallAvailabilityTests : IClassFixture<SimfApiFactory>
{
    // Far-future windows so the slots are in the future regardless of the test clock.
    private static readonly DateTime WindowStart = new(2030, 1, 1, 10, 0, 0);

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
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var windows = await GetWindowsAsync(hallId, admin);
        Assert.Single(windows);

        var slots = await GetSlotsAsync(hallId, admin);
        Assert.Equal(2, slots.Count);
        Assert.Equal(WindowStart, slots[0].Start);
        Assert.Equal(WindowStart.AddMinutes(30), slots[0].End);
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
                Start = WindowStart, End = WindowStart.AddMinutes(-10), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Unknown hall → 404.
        var unknown = await PostAuthAsync(
            $"/api/v1/admin/halls/{Guid.NewGuid()}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
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
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        var window = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminHallAvailabilityWindow>>())!.Data!;

        var del = await SendAuthAsync(HttpMethod.Delete,
            $"/api/v1/admin/hall-availability-windows/{window.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        Assert.Empty(await GetWindowsAsync(hallId, admin));
        Assert.Empty(await GetSlotsAsync(hallId, admin));
    }

    [Fact]
    public async Task A_bound_meeting_removes_its_slot_from_available_slots()
    {
        // E2E-HAV-004 (D-716, GAP-2) — a slot taken by a bound meeting (a
        // SpeakerMeetingRequest in AwaitingSpeaker/Accepted) drops out of the hall's
        // free slots; the other slots stay offered.
        var admin = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync();
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var before = await GetSlotsAsync(hallId, admin);
        Assert.Equal(2, before.Count);

        await SeedBoundMeetingAsync(hallId, before[0].Start, before[0].End);

        var after = await GetSlotsAsync(hallId, admin);
        Assert.Single(after);
        Assert.Equal(before[1].Start, after[0].Start);
    }

    [Fact]
    public async Task A36_hall_availability_is_gated_by_its_own_permission_not_the_speaker_desk()
    {
        // QA A36 — the four endpoints used to borrow SpeakerMeetingRequests.*, so
        // a delegation-meeting operator or a halls operator could never define the
        // windows that EVERY meeting Approve modal reads. They now carry the
        // hall-scoped HallAvailability pair.
        var hallId = await SeedHallAsync();

        // The speaker desk alone no longer opens the hall's windows.
        var speakerDeskOnly = await CreateAdminWithCustomRoleAsync(
        [
            PermissionCatalog.SpeakerMeetingRequests.View,
            PermissionCatalog.SpeakerMeetingRequests.Manage,
        ]);
        var denied = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, speakerDeskOnly);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // A hall-availability operator holding NO meeting-desk code at all can
        // define a window and read the free slots both desks depend on.
        var hallOperator = await CreateAdminWithCustomRoleAsync(
        [
            PermissionCatalog.HallAvailability.View,
            PermissionCatalog.HallAvailability.Manage,
        ]);
        var created = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = WindowStart, End = WindowStart.AddMinutes(60), SlotMinutes = 30,
            }, hallOperator);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var slots = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/halls/{hallId}/available-slots", hallOperator);
        Assert.Equal(HttpStatusCode.OK, slots.StatusCode);

        // View alone reads but never writes.
        var readOnly = await CreateAdminWithCustomRoleAsync(
            [PermissionCatalog.HallAvailability.View]);
        var listed = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/halls/{hallId}/availability-windows", readOnly);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var write = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/availability-windows",
            new CreateHallAvailabilityWindowRequest
            {
                Start = WindowStart.AddDays(1), End = WindowStart.AddDays(1).AddMinutes(60),
                SlotMinutes = 30,
            }, readOnly);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    // A UserType.Admin user holding a fresh custom role whose only grants are
    // `codes` (mirrors PermissionEnforcementTests — the seeder does not run under
    // the Testing host, so the Permission rows are inserted here).
    private async Task<string> CreateAdminWithCustomRoleAsync(string[] codes)
    {
        var email = $"hall-avail-perm-{Guid.NewGuid():N}@simf.test";
        var roleName = $"HallAvail-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in codes)
            {
                var def = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions
                    .SingleOrDefaultAsync(p => p.Code == code);
                if (permission is null)
                {
                    permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Code = def.Code,
                    };
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Hall Avail Limited",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }


    private async Task SeedBoundMeetingAsync(
        Guid hallId, DateTime start, DateTime end)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Bound Speaker", NameArabic = "متحدّث",
            AllowsMeetingRequests = true, IsActive = true, DisplayOrder = 0,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        db.SpeakerMeetingRequests.Add(new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Bound", Subject = "Bound meeting",
            HallId = hallId, SlotStart = start, SlotEnd = end,
            Status = MeetingRequestStatus.AwaitingSpeaker,
            CreatedAt = SimfClock.Now, RespondedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

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
            Capacity = 20, IsActive = true, CreatedAt = SimfClock.Now,
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
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
