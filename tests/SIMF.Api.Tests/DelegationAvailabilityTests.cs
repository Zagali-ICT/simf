// Tests: Bi-Meeting rework — delegation availability windows + free slots
// (parity with SpeakerAvailabilityTests).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class DelegationAvailabilityTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationAvailabilityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_window_then_it_lists_and_yields_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var countryId = await SeedInvitedCountryAsync();
        var start = await ForumStartAsync(admin);

        var create = await PostAuthAsync(
            $"/api/v1/admin/countries/{countryId}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var windows = await GetWindowsAsync(countryId, admin);
        Assert.Single(windows);

        var slots = await GetSlotsAsync(countryId, admin);
        Assert.Equal(2, slots.Count);
        Assert.Equal(start, slots[0].Start);
        Assert.Equal(start.AddMinutes(30), slots[0].End);
    }

    [Fact]
    public async Task A_live_delegation_meeting_slot_is_excluded_from_the_free_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var countryId = await SeedInvitedCountryAsync();
        var otherCountryId = await SeedInvitedCountryAsync();
        var start = await ForumStartAsync(admin);
        await PostAuthAsync(
            $"/api/v1/admin/countries/{countryId}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);

        // A Done meeting still HOLDS its slot (SlotHolding) — assert the terminal state
        // is excluded too, covering the P0 SlotHolding change.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.DelegationMeetingRequests.Add(new DelegationMeetingRequest
            {
                Id = Guid.NewGuid(),
                RequestedByUserId = Guid.NewGuid(),
                RequestingCountryId = otherCountryId,
                TargetCountryId = countryId,
                AttendeeCount = 2,
                Subject = "Slot held",
                Status = MeetingRequestStatus.Done,
                SlotStart = start,
                SlotEnd = start.AddMinutes(30),
                CreatedAt = SimfClock.Now,
            });
            await db.SaveChangesAsync();
        }

        var slots = await GetSlotsAsync(countryId, admin);
        Assert.Single(slots);
        Assert.Equal(start.AddMinutes(30), slots[0].Start); // only the 2nd slot remains
    }

    [Fact]
    public async Task An_invalid_window_is_400_and_a_non_invited_country_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var countryId = await SeedInvitedCountryAsync();
        var start = await ForumStartAsync(admin);

        // End before start → 400.
        var bad = await PostAuthAsync(
            $"/api/v1/admin/countries/{countryId}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(-10), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Non-invited country → 400 DelegateCountryNotInvited.
        var notInvited = await SeedCountryAsync(invited: false);
        var resp = await PostAuthAsync(
            $"/api/v1/admin/countries/{notInvited}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DelegateCountryNotInvited, body.Error!.Code);
    }

    [Fact]
    public async Task Delete_window_removes_its_slots()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var countryId = await SeedInvitedCountryAsync();
        var start = await ForumStartAsync(admin);
        var create = await PostAuthAsync(
            $"/api/v1/admin/countries/{countryId}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = start, End = start.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        var window = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminDelegationAvailabilityWindow>>())!.Data!;

        var del = await SendAuthAsync(HttpMethod.Delete,
            $"/api/v1/admin/delegation-availability-windows/{window.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        Assert.Empty(await GetWindowsAsync(countryId, admin));
        Assert.Empty(await GetSlotsAsync(countryId, admin));
    }

    [Fact]
    public async Task Create_window_outside_the_forum_window_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var countryId = await SeedInvitedCountryAsync();
        // 20 days after the last forum day — safely outside the bound.
        var outside = (await ForumEndAsync(admin)).AddDays(20);

        var resp = await PostAuthAsync(
            $"/api/v1/admin/countries/{countryId}/availability-windows",
            new CreateDelegationAvailabilityWindowRequest
            {
                Start = outside, End = outside.AddMinutes(60), SlotMinutes = 30,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    // The forum-day bound is dynamic (MIN/MAX over the seeded ProgrammeDay rows, which
    // other test classes mutate on the shared DB). Read it at test time and place the
    // window on the first forum day at 10:00 UTC (+03:00 event day == MinDate).
    private async Task<DateTime> ForumStartAsync(string admin)
    {
        var win = await ForumWindowAsync(admin);
        var min = win.MinDate ?? new DateOnly(2026, 11, 24);
        return new DateTime(min.Year, min.Month, min.Day, 10, 0, 0);
    }

    private async Task<DateTime> ForumEndAsync(string admin)
    {
        var win = await ForumWindowAsync(admin);
        var max = win.MaxDate ?? new DateOnly(2026, 11, 24);
        return new DateTime(max.Year, max.Month, max.Day, 10, 0, 0);
    }

    private async Task<ForumWindowResponse> ForumWindowAsync(string admin)
    {
        var r = await SendAuthAsync(HttpMethod.Get, "/api/v1/admin/programme/forum-window", admin);
        return (await r.Content.ReadFromJsonAsync<ApiResult<ForumWindowResponse>>())!.Data!;
    }

    private async Task<IReadOnlyList<AdminDelegationAvailabilityWindow>> GetWindowsAsync(
        int countryId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/admin/countries/{countryId}/availability-windows", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminDelegationAvailabilityWindow>>>())!.Data!;
    }

    private async Task<IReadOnlyList<DelegationAvailableSlot>> GetSlotsAsync(int countryId, string token)
    {
        var r = await SendAuthAsync(HttpMethod.Get,
            $"/api/v1/app/countries/{countryId}/available-slots", token);
        return (await r.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<DelegationAvailableSlot>>>())!.Data!;
    }

    // Country.Id is a manually-assigned ISO numeric (ValueGeneratedNever) and Code is a
    // unique 2-char ISO alpha-2. Seed a FRESH country per call — a high non-ISO Id and a
    // digit-bearing 2-char code (no real ISO alpha-2 contains a digit, so it can never
    // collide with the seeded list) — so each test's windows are isolated by countryId.
    private static int _seq;

    private Task<int> SeedInvitedCountryAsync() => SeedCountryAsync(invited: true);

    private async Task<int> SeedCountryAsync(bool invited)
    {
        var n = System.Threading.Interlocked.Increment(ref _seq);
        var id = 950000 + n;
        var code = $"{(char)('Y' + (n / 10) % 2)}{n % 10}"; // Y0..Y9, Z0..Z9 — never ISO
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.Countries.Add(new SIMF.Domain.Common.Country
        {
            Id = id,
            Code = code,
            Name = "Avail Country " + code, NameArabic = "دولة " + code,
            IsActive = true,
            IsInvited = invited,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"davail-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Delegation Avail Admin",
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
