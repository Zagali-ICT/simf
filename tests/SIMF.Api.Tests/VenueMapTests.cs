// P2.5 — D-230 (FR-605, FDS-006 §5.3): 2D venue-map node CRUD + the public
// read the app renders. Mirrors SystemSettingsTests.
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

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class VenueMapTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public VenueMapTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_list_and_public_read()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var label = "Main Hall " + Guid.NewGuid().ToString("N")[..6];
        var create = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = label, LabelArabic = "القاعة الرئيسية",
                Kind = VenueMapNodeKind.Zone, X = 120.5, Y = 88.0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminVenueMapNodeDetail>>())!.Data!;
        Assert.Equal(label, created.Label);
        Assert.Equal(120.5, created.X);

        var get = await GetAuthAsync($"/api/v1/admin/venue-map/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // Public read (AllowAnonymous) returns the active node.
        var pub = await _client.GetAsync("/api/v1/app/venue-map");
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);
        var nodes = (await pub.Content
            .ReadFromJsonAsync<ApiResult<List<PublicVenueMapNode>>>())!.Data!;
        Assert.Contains(nodes, n => n.Id == created.Id);
    }

    [Fact]
    public async Task Create_with_a_kind_that_mismatches_its_reference_is_400()
    {
        // D-611 (Wave B) — a Hall reference requires Kind=Hall; a Zone node that
        // also carries a HallId is rejected at the edge (400) rather than faulting
        // CK_VenueMapNodes_KindArc (500). Seed a real Hall so the existence check
        // passes and the kind-arc guard is what fires.
        var token = await CreateAdministratorAndSignInAsync();
        Guid hallId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var hall = new Hall
            {
                Id = Guid.NewGuid(),
                Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = "Arc Hall",
                NameArabic = "قاعة القوس",
                Capacity = 100,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.Halls.Add(hall);
            await db.SaveChangesAsync();
            hallId = hall.Id;
        }

        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = "Bad", LabelArabic = "خطأ",
                Kind = VenueMapNodeKind.Zone, X = 1, Y = 1,
                HallId = hallId, // mismatched: a Zone node must not carry a Hall ref
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_an_unknown_hall_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = "Bad", LabelArabic = "خطأ",
                Kind = VenueMapNodeKind.Hall, X = 1, Y = 1,
                HallId = Guid.NewGuid(),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.VenueMapNodeInvalid, body.Error!.Code);
    }

    // #25 — an out-of-range Kind (an undefined enum value from a direct API call)
    // is rejected with a clean 400 instead of persisting an invalid node.
    [Fact]
    public async Task Create_with_an_undefined_kind_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = "Bad Kind", LabelArabic = "نوع خاطئ",
                Kind = (VenueMapNodeKind)99, X = 1, Y = 1,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.VenueMapNodeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Deactivate_drops_it_from_the_public_read()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = "Temp", LabelArabic = "مؤقت",
                Kind = VenueMapNodeKind.PointOfInterest, X = 5, Y = 5,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminVenueMapNodeDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/venue-map/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var pub = await _client.GetAsync("/api/v1/app/venue-map");
        var nodes = (await pub.Content
            .ReadFromJsonAsync<ApiResult<List<PublicVenueMapNode>>>())!.Data!;
        Assert.DoesNotContain(nodes, n => n.Id == created.Id);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = "Nope", LabelArabic = "لا", Kind = VenueMapNodeKind.Zone, X = 0, Y = 0,
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"venue-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Venue Admin",
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
