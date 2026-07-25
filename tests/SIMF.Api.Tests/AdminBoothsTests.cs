// D-199 — admin CRUD over Booth (Exhibition module, Mockup page 22 + 2D map).
// Mirrors AdminSpeakersTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.Files;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Venue;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminBoothsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminBoothsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_returns_the_booth()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = code,
                Name = "Advanced Naval Technologies",
                NameArabic = "تقنيات بحرية متقدمة",
                ExhibitorId = exhibitorId,
                OfficerName = "Capt. Khalid",
                OfficerPhone = "+966500000000",
                OfficerEmail = "khalid@booth.test",
                Sector = "Defense Systems",
                Description = "Cutting-edge naval defense systems.",
                MapX = 12.5,
                MapY = 8.0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;
        Assert.Equal(code, created.Code);
        Assert.Equal(exhibitorId, created.ExhibitorId);
        Assert.Equal("Capt. Khalid", created.OfficerName);
        Assert.Equal(12.5, created.MapX);
        Assert.True(created.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(exhibitorId, fetched.ExhibitorId);
        Assert.Equal("Defense Systems", fetched.Sector);
    }

    [Fact]
    public async Task Create_with_an_unknown_exhibitor_is_a_400_BOOTH_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), Name = "X", NameArabic = "س",
                ExhibitorId = Guid.NewGuid(),   // never seeded
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BoothInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Create_with_an_inactive_exhibitor_is_a_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var dormant = await SeedExhibitorAsync(isActive: false);
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), Name = "X", NameArabic = "س", ExhibitorId = dormant,
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_malformed_officer_email_is_a_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), Name = "X", NameArabic = "س",
                OfficerEmail = "not-an-email",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_booth_sources_its_exhibitor_name_from_the_linked_exhibitor()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"Co{Guid.NewGuid():N}"[..8];
        var exhibitorId = await SeedExhibitorAsync(nameEn: $"{marker} Industries");
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = NewCode(), Name = "Hull 7", NameArabic = "بدن 7", ExhibitorId = exhibitorId,
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var publicList = await _client.GetAsync("/api/v1/app/booths");
        var list = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        var row = Assert.Single(list, b => b.Id == created.Id);
        // The wire field ExhibitorName is now populated from the Exhibitor.
        Assert.Equal($"{marker} Industries", row.ExhibitorName);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_BOOTH_CODE_DUPLICATE()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var first = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, Name = "A", NameArabic = "أ" },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, Name = "B", NameArabic = "ب" },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BoothCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/booths/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_hides_the_booth_from_the_public_list_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = code, Name = "D", NameArabic = "د" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        var first = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // idempotent

        // The public anonymous list must no longer contain the deactivated booth.
        var publicList = await _client.GetAsync("/api/v1/app/booths");
        var list = (await publicList.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<PublicBoothSummary>>>())!.Data!;
        Assert.DoesNotContain(list, b => b.Id == created.Id);
    }

    // #26 — a booth still marked by an active venue-map node cannot be
    // deactivated (that would orphan the map node); the admin gets a 409
    // BOOTH_IN_USE telling them to remove the node first.
    [Fact]
    public async Task Deactivate_is_blocked_when_a_venue_map_node_still_marks_the_booth()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = NewCode(), Name = "Mapped", NameArabic = "معلّم" },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.Set<VenueMapNode>().Add(new VenueMapNode
            {
                Id = Guid.NewGuid(),
                Label = "Booth marker",
                LabelArabic = "علامة الجناح",
                Kind = VenueMapNodeKind.Booth,
                X = 10,
                Y = 20,
                BoothId = created.Id,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var delete = await DeleteAuthAsync($"/api/v1/admin/booths/{created.Id}", token);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var body = (await delete.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BoothInUse, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest { Code = NewCode(), Name = "F", NameArabic = "ف" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCode() => "B-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    [Fact]
    public async Task Booth_list_reports_HasBoothLogo_from_the_booths_own_logo()
    {
        // A booth now owns its own BoothLogo (owner = the booth id). The grid
        // renders that own-logo thumbnail when the booth has an active BoothLogo
        // asset, else an initials tile. Guards the batched per-owner
        // flag-population path. (The shared Contact directory was removed;
        // ExhibitorContactId/HasLogo are append-only frozen wire fields that now
        // always emit null/false.)
        var token = await CreateAdministratorAndSignInAsync();

        var exhibitorId = await SeedExhibitorAsync();

        var codeWith = NewCode();
        var codeWithout = NewCode();
        var codeOrphan = NewCode();
        var boothWithId = await CreateBoothAsync(codeWith, exhibitorId, token);
        await CreateBoothAsync(codeWithout, exhibitorId, token);
        // A booth with NO exhibitor at all — must still appear (a booth with no
        // exhibitor is still listed; the logo flag is booth-owned, not joined).
        await CreateBoothAsync(codeOrphan, exhibitorId: null, token);

        // Give only the first booth its own active BoothLogo (owner = the booth).
        await SeedBoothLogoAsync(boothWithId);

        var rows = await ListBoothsAsync(token);
        var boothWith = rows.Single(b => b.Code == codeWith);
        var boothWithout = rows.Single(b => b.Code == codeWithout);
        var boothOrphan = rows.Single(b => b.Code == codeOrphan);

        // The booth with an active BoothLogo → HasBoothLogo true.
        Assert.True(boothWith.HasBoothLogo);
        // The booth without its own BoothLogo → initials fallback.
        Assert.False(boothWithout.HasBoothLogo);
        // The exhibitor-less booth still lists, and owns no logo.
        Assert.Null(boothOrphan.ExhibitorId);
        Assert.False(boothOrphan.HasBoothLogo);

        // Append-only frozen wire fields — always null/false since the shared
        // Contact directory was removed.
        Assert.Null(boothWith.ExhibitorContactId);
        Assert.False(boothWith.HasLogo);
    }

    private async Task<Guid> CreateBoothAsync(string code, Guid? exhibitorId, string token)
    {
        var create = await PostAuthAsync(
            "/api/v1/admin/booths",
            new AdminCreateBoothRequest
            {
                Code = code,
                Name = "Booth " + code,
                NameArabic = "جناح",
                ExhibitorId = exhibitorId,
                // Booth-officer fields are inline columns on the booth now.
                OfficerName = "Officer",
                OfficerPhone = "+966500000000",
                OfficerEmail = "officer@booth.test",
                Sector = "Naval",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminBoothDetail>>())!.Data!;
        return created.Id;
    }

    private async Task<IReadOnlyList<AdminBoothSummary>> ListBoothsAsync(string token)
    {
        var list = await PostAuthAsync(
            "/api/v1/admin/booths/list", new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        return (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBoothSummary>>>())!.Data!.Items;
    }

    // Seeds an active BoothLogo asset owned by the booth itself (owner = the
    // booth id, FileService.BoothLogo) — so the grid resolves the booth's own
    // logo flag (HasBoothLogo) to true.
    private async Task SeedBoothLogoAsync(Guid boothId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.Set<StoredFile>().Add(new StoredFile
        {
            Id = Guid.NewGuid(),
            Service = FileService.BoothLogo,
            OwnerEntityId = boothId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await appDb.SaveChangesAsync();
    }

    // B1 — D-222: seed an exhibitor so a booth can reference it. Defaults to an
    // active exhibitor.
    private async Task<Guid> SeedExhibitorAsync(
        bool isActive = true,
        string? nameEn = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = nameEn ?? $"Exhibitor {Guid.NewGuid():N}",
            NameArabic = "شركة عارضة",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Exhibitors.Add(exhibitor);
        await appDb.SaveChangesAsync();
        return exhibitor.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"booth-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Booth Admin",
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
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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
