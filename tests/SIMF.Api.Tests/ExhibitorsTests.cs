// D-357 / D-260 — regression coverage for the Admins/Exhibitors-list logo thumbnail.
// The shared Contact directory was removed; an exhibitor now carries its own inline
// contact/identity columns (City, CountryId, ...) and owns its OWN logo — an active
// ExhibitorLogo asset (owner = the exhibitor's id, FileService.ExhibitorLogo). The
// grid reports that via AdminExhibitorSummary.HasExhibitorLogo (no ContactId, no
// Contact hop). This guards the batched WhichOwnersHaveActiveAssetAsync projection
// in AdminExhibitorService and satisfies its // Tests: header.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Venue;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ExhibitorsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exhibitor_list_reports_HasExhibitorLogo_from_its_own_active_logo()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"EX{Guid.NewGuid():N}"[..10];

        // Two paths: an exhibitor that owns an active ExhibitorLogo, and one with no
        // logo at all. The resolve is a null-safe batched lookup (not an inner join
        // that would hide the logo-less row), so both must still list.
        var withLogo = await SeedExhibitorAsync($"{marker}-with", withLogo: true);
        var noLogo = await SeedExhibitorAsync($"{marker}-none", withLogo: false);

        var rows = await ListExhibitorsAsync(token);
        var rowWith = rows.Single(e => e.Id == withLogo);
        var rowNoLogo = rows.Single(e => e.Id == noLogo);

        // Owns an active ExhibitorLogo → HasExhibitorLogo true (grid renders the thumbnail).
        Assert.True(rowWith.HasExhibitorLogo);
        // No logo → initials fallback (HasExhibitorLogo false), row still listed.
        Assert.False(rowNoLogo.HasExhibitorLogo);
    }

    // Retiring the company standing in a booth is the same orphan the BOOTH_IN_USE
    // guard refuses, reached one level up: the public booth projection drops a booth
    // whose Exhibitor is inactive, so the venue map would keep drawing a pin the
    // public endpoint no longer serves. The admin gets a 409 naming the booth whose
    // node has to go first.
    [Fact]
    public async Task Deactivate_is_blocked_when_a_booth_of_this_exhibitor_is_on_the_venue_map()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorAsync(
            $"EX{Guid.NewGuid():N}"[..10], withLogo: false);
        var boothId = await SeedBoothAsync(exhibitorId);
        await PinBoothOnVenueMapAsync(boothId);

        var delete = await DeleteAuthAsync($"/api/v1/admin/exhibitors/{exhibitorId}", token);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ExhibitorInUse, body.Error!.Code);
        // A refused delete must not have half-committed: the row is still live.
        Assert.True(await IsExhibitorActiveAsync(exhibitorId));
    }

    // The guard is scoped to THIS exhibitor's own booths. Seeding a pinned booth
    // under a different exhibitor proves it, because a guard that merely asked
    // "is any node active?" would pass the test above and still wrongly block
    // every deactivate in the system once one pin existed anywhere.
    [Fact]
    public async Task Deactivate_succeeds_when_no_booth_of_this_exhibitor_is_on_the_venue_map()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"EX{Guid.NewGuid():N}"[..10];

        var otherExhibitorId = await SeedExhibitorAsync($"{marker}-other", withLogo: false);
        await PinBoothOnVenueMapAsync(await SeedBoothAsync(otherExhibitorId));

        // This exhibitor owns a booth too, just no venue-map node marking it.
        var exhibitorId = await SeedExhibitorAsync($"{marker}-free", withLogo: false);
        await SeedBoothAsync(exhibitorId);

        var delete = await DeleteAuthAsync($"/api/v1/admin/exhibitors/{exhibitorId}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.False(await IsExhibitorActiveAsync(exhibitorId));

        // Still idempotent: the guard sits behind the already-inactive early return,
        // so a repeat delete answers 200 rather than falling into the 409.
        var second = await DeleteAuthAsync($"/api/v1/admin/exhibitors/{exhibitorId}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<IReadOnlyList<AdminExhibitorSummary>> ListExhibitorsAsync(string token)
    {
        var list = await PostAuthAsync(
            "/api/v1/admin/exhibitors/list", new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        return (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminExhibitorSummary>>>())!.Data!.Items;
    }

    // Seeds an active exhibitor with its inline identity columns (City/CityArabic),
    // optionally owning an active ExhibitorLogo asset (owner = the exhibitor's id,
    // FileService.ExhibitorLogo) so HasExhibitorLogo resolves true. Mirrors how
    // AssetEndpointsTests seeds an owned logo row.
    private async Task<Guid> SeedExhibitorAsync(string nameEn, bool withLogo)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitorId = Guid.NewGuid();
        appDb.Exhibitors.Add(new Exhibitor
        {
            Id = exhibitorId,
            Name = nameEn,
            NameArabic = "عارض",
            City = "Jeddah",
            CityArabic = "جدة",
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        if (withLogo)
        {
            appDb.Set<StoredFile>().Add(new StoredFile
            {
                Id = Guid.NewGuid(),
                Service = FileService.ExhibitorLogo,
                OwnerEntityId = exhibitorId,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
        }
        await appDb.SaveChangesAsync();
        return exhibitorId;
    }

    // A booth standing in this exhibitor's space. Seeded straight onto the context
    // rather than through the admin API because the booth's own create path is
    // AdminBoothsTests' subject, not this one's; Code is unique across active and
    // inactive booths alike, so it carries a Guid marker.
    private async Task<Guid> SeedBoothAsync(Guid exhibitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var boothId = Guid.NewGuid();
        appDb.Booths.Add(new Booth
        {
            Id = boothId,
            Code = "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Stand",
            NameArabic = "جناح",
            ExhibitorId = exhibitorId,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return boothId;
    }

    private async Task PinBoothOnVenueMapAsync(Guid boothId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.VenueMapNodes.Add(new VenueMapNode
        {
            Id = Guid.NewGuid(),
            Label = "Booth marker",
            LabelArabic = "علامة الجناح",
            Kind = VenueMapNodeKind.Booth,
            X = 10,
            Y = 20,
            BoothId = boothId,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
    }

    // Read back from the database rather than from the grid: the grid's default
    // filter could hide an inactive row, which would make a failed guard look like
    // a passing test.
    private async Task<bool> IsExhibitorActiveAsync(Guid exhibitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.Exhibitors
            .AsNoTracking()
            .Where(exhibitor => exhibitor.Id == exhibitorId)
            .Select(exhibitor => exhibitor.IsActive)
            .SingleAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"exhibitor-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Exhibitor Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
