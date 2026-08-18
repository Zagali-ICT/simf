// The yearly lifecycle: an admin opens a year, content and registrations
// accumulate against it, then the year is closed into history and the next opens.
//
// Two halves that are easy to confuse, and the confusion is the point of these
// tests. The gate REFUSES a badge from a closed edition — but refusing it is
// only correct if the holder has a route to a live one, so opening a year also
// CLEARS every badge for re-issue. A build with only the first half ships an
// event where every returning attendee is turned away at the door with nothing
// anyone can do about it.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Editions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class EventEditionTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public EventEditionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Opening_a_year_reissues_every_badge_and_reports_how_many()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var openYear = await OpenYearAsync();

        // Two attendees holding badges in the open year.
        var first = await SeedAttendeeWithBadgeAsync(openYear);
        var second = await SeedAttendeeWithBadgeAsync(openYear);

        // The badges they hold BEFORE the year opens, so the assertion below can
        // tell a re-issue from a row that simply kept what it had.
        var oldBadges = await BadgesOfAsync(first, second);

        var response = await PostAuthAsync(
            "/api/v1/admin/editions/open",
            new AdminOpenEditionRequest { Year = openYear + 1 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminOpenEditionResponse>>())!.Data!;

        Assert.Equal(openYear + 1, result.Year);
        Assert.True(result.BadgesCleared >= 2, "both seeded badges should have been cleared");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        foreach (var profileId in new[] { first, second })
        {
            var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
            // Re-issued, not just cleared. Badges reach attendees by email and by
            // being printed at the desk, and both read the QR off this row: leaving
            // it null hands the operator a table of blanks, and an already-approved
            // attendee has no route back to a badge - ApproveAsync refuses anything
            // not PendingApproval and the bulk mint only creates new rows.
            Assert.False(string.IsNullOrEmpty(profile.QrId));
            Assert.NotEqual(oldBadges[profileId], profile.QrId);
            // The year moves WITH the badge. It used to stay behind - EditionYear
            // was written once at insert - and the gate then refused the new badge
            // as outside its year.
            Assert.Equal(openYear + 1, profile.EditionYear);
            Assert.Equal(AccountState.Approved, profile.AdmissionState);
        }
    }

    [Fact]
    public async Task A_new_attendee_is_stamped_with_the_open_year()
    {
        var openYear = await OpenYearAsync();
        var profileId = await SeedAttendeeWithBadgeAsync(editionYear: 0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.Id == profileId);
        // Stamped by the interceptor, not by the caller — which is the point:
        // a dozen paths create attendees and none of them has to remember.
        Assert.Equal(openYear, profile.EditionYear);
    }

    [Fact]
    public async Task Re_opening_the_same_year_is_refused()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var openYear = await OpenYearAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/editions/open",
            new AdminOpenEditionRequest { Year = openYear }, token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Going_back_to_an_earlier_year_is_refused()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var openYear = await OpenYearAsync();

        // Re-opening a closed year would make every badge issued since valid
        // again at the gate, which is the opposite of what closing it meant.
        var response = await PostAuthAsync(
            "/api/v1/admin/editions/open",
            new AdminOpenEditionRequest { Year = openYear - 1 }, token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_year_outside_the_encodable_range_is_refused()
    {
        var token = await CreateAdministratorAndSignInAsync();

        // The year is encoded into the badge as two bytes, so a typo has to be
        // refused before it is printed onto anything.
        var response = await PostAuthAsync(
            "/api/v1/admin/editions/open",
            new AdminOpenEditionRequest { Year = 202 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_current_edition_is_readable()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var openYear = await OpenYearAsync();

        var response = await GetAuthAsync("/api/v1/admin/editions/current", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminEventEditionResponse>>())!;
        Assert.True(body.Success);
        Assert.Equal(openYear, body.Data!.Year);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>The year currently open, read straight from the singleton.</summary>
    private async Task<int> OpenYearAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.EventEdition.AsNoTracking()
            .Where(e => e.Id == EventEdition.SingletonId)
            .Select(e => e.Year)
            .SingleAsync();
    }

    /// <summary>An approved attendee holding a badge. Pass 0 to leave the edition
    /// year unset so the stamping interceptor fills it.</summary>
    private async Task<Dictionary<Guid, string?>> BadgesOfAsync(params Guid[] profileIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.UserProfiles.AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.QrId);
    }

    private async Task<Guid> SeedAttendeeWithBadgeAsync(int editionYear)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profileId = await TestAttendeeProfiles.CreateAccountlessAsync(
            db, TestAttendeeProfiles.NewQrId());
        if (editionYear != 0)
        {
            var profile = await db.UserProfiles.SingleAsync(p => p.Id == profileId);
            profile.EditionYear = editionYear;
            await db.SaveChangesAsync();
        }
        return profileId;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        const string AdministratorRole = "Administrator";
        var email = $"edition-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<SIMF.Domain.IdentityAccess.SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SIMF.Domain.IdentityAccess.SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<SIMF.Domain.IdentityAccess.SimfUser>>();
            var user = new SIMF.Domain.IdentityAccess.SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Edition Test Admin",
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
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
