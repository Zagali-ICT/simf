// Tests: D-499 (الوفود, Figma 1426:10771) — the public delegations view
// (GET /app/delegations) + the CP head-of-delegation / dates on the country form
// (PublicDelegationService, AdminCountryService, CountryEndpoints).
// Tests: G2 (D-811) — the per-viewer exclusion of the caller's own country.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Delegations;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-499 (الوفود) — the public delegations view groups the invited countries with
/// their head of delegation, date range and member count; the CP country form
/// sets the dates + head (an active delegate of that country). G2 (D-811) adds the
/// per-viewer rule: a signed-in caller never sees their own country, and the two
/// aggregate stats are recomputed over the filtered list.
/// </summary>
public sealed class DelegationsTests : IClassFixture<SimfApiFactory>
{
    // ISO numeric ids are hand-assigned and the fixture database accumulates
    // across the tests of this class, so picking one at random from the 900..988
    // band — 89 slots shared by every country these tests create — eventually
    // collides. When it did, the server's ID-duplicate check fired BEFORE the
    // rule the test was actually asserting and the failure read like a product
    // bug: Admin_update_persists_the_invited_flag_dates_and_head reported a 409
    // where it expected 200. A counter makes each id unique within the run
    // rather than merely probably-unique. Same fix, and same reason, as
    // AdminCountriesTests.
    private static int _nextTestCountryId = 900;

    private static int ClaimCountryId() => Interlocked.Increment(ref _nextTestCountryId);

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Delegations_is_anonymous_and_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/app/delegations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Lists_an_invited_country_with_its_head_dates_and_member_count()
    {
        // A VISITING delegation (Japan), invited, with a designated head + two more
        // delegates. Deliberately not Saudi Arabia: KSA is the OWNER of the forum, not
        // a visiting delegation, so it is never flagged Country.IsInvited (D-768) —
        // and under G2 (D-811) the host's own visitors would not see it anyway.
        await EnsureCountryAsync("JP", 392);
        var headId = await SeedDelegateAsync(
            392,
            name: "Admiral Kenji Sato",
            nameArabic: "الأدميرال كينجي ساتو",
            jobTitle: "قائد القوات البحرية");
        await SeedDelegateAsync(392);
        await SeedDelegateAsync(392);
        await SetCountryDelegationAsync(
            392,
            invited: true,
            arrival: new DateOnly(2026, 1, 12),
            departure: new DateOnly(2026, 1, 15),
            headProfileId: headId);

        var data = await GetDelegationsAsync();
        var item = Assert.Single(data.Items, i => i.CountryId == 392);
        Assert.Equal(3, item.MemberCount);
        Assert.Equal("Admiral Kenji Sato", item.HeadName);
        Assert.Equal("الأدميرال كينجي ساتو", item.HeadNameArabic);
        Assert.Equal("قائد القوات البحرية", item.HeadTitle);
        Assert.Equal(new DateOnly(2026, 1, 12), item.ArrivalDate);
        Assert.Equal(new DateOnly(2026, 1, 15), item.DepartureDate);
        Assert.Equal("JP", item.CountryCode);
        Assert.True(data.CountryCount >= 1);
        Assert.True(data.TotalParticipants >= 3);
    }

    [Fact]
    public async Task A_signed_in_viewer_does_not_see_their_own_delegation_but_a_guest_sees_all()
    {
        // G2 (D-811) — the viewer's OWN country (their UserProfile.NationalityId) is
        // filtered out server-side, and both stats are recomputed over what is shown.
        // South Korea = the viewer's nationality; Singapore = another delegation.
        await EnsureCountryAsync("KR", 410);
        await EnsureCountryAsync("SG", 702);
        await SeedDelegateAsync(410);
        await SeedDelegateAsync(410);
        await SeedDelegateAsync(702);
        await SetCountryDelegationAsync(410, invited: true);
        await SetCountryDelegationAsync(702, invited: true);
        var viewerToken = await CreateVisitorWithNationalityAsync(410);

        // (c) an anonymous caller sees the full list — nothing is excluded.
        var guest = await GetDelegationsAsync();
        var ownCountry = Assert.Single(guest.Items, i => i.CountryId == 410);
        Assert.Contains(guest.Items, i => i.CountryId == 702);
        // (d) the guest's stats match the full list.
        Assert.Equal(guest.Items.Count, guest.CountryCount);
        Assert.Equal(guest.Items.Sum(i => i.MemberCount), guest.TotalParticipants);

        var viewer = await GetDelegationsAsync(viewerToken);
        // (a) the viewer's own delegation is gone...
        Assert.DoesNotContain(viewer.Items, i => i.CountryId == 410);
        // (b) ...and every other invited delegation is still there.
        Assert.Contains(viewer.Items, i => i.CountryId == 702);
        // (d) the viewer's stats match the FILTERED list — exactly one country and
        // its members dropped out relative to the guest view.
        Assert.Equal(viewer.Items.Count, viewer.CountryCount);
        Assert.Equal(viewer.Items.Sum(i => i.MemberCount), viewer.TotalParticipants);
        Assert.Equal(guest.CountryCount - 1, viewer.CountryCount);
        Assert.Equal(
            guest.TotalParticipants - ownCountry.MemberCount, viewer.TotalParticipants);
    }

    [Fact]
    public async Task A_signed_in_caller_with_no_profile_sees_every_invited_delegation()
    {
        // G2 (D-811) — an Admin / CP user carries no UserProfile, so there is no
        // nationality to exclude and the full list is returned (no over-filtering).
        await EnsureCountryAsync("ID", 360);
        await SeedDelegateAsync(360);
        await SetCountryDelegationAsync(360, invited: true);
        var adminToken = await CreateAdministratorAndSignInAsync();

        var data = await GetDelegationsAsync(adminToken);
        Assert.Contains(data.Items, i => i.CountryId == 360);
        Assert.Equal(data.Items.Count, data.CountryCount);
        Assert.Equal(data.Items.Sum(i => i.MemberCount), data.TotalParticipants);
    }

    [Fact]
    public async Task A_country_that_is_not_invited_is_excluded()
    {
        await EnsureCountryAsync("US", 840);
        await SeedDelegateAsync(840);
        await SetCountryDelegationAsync(840, invited: false);

        var data = await GetDelegationsAsync();
        Assert.DoesNotContain(data.Items, i => i.CountryId == 840);
    }

    [Fact]
    public async Task Member_count_excludes_inactive_and_non_delegate_profiles()
    {
        await EnsureCountryAsync("KW", 414);
        await SeedDelegateAsync(414); // counts
        await SeedDelegateAsync(414, isActive: false); // inactive — excluded
        await SeedDelegateAsync(414, isDelegate: false); // not a delegate — excluded
        await SetCountryDelegationAsync(414, invited: true);

        var data = await GetDelegationsAsync();
        var item = Assert.Single(data.Items, i => i.CountryId == 414);
        Assert.Equal(1, item.MemberCount);
        // No head designated — the head fields stay null.
        Assert.Null(item.HeadName);
        Assert.Null(item.HeadTitle);
    }

    [Fact]
    public async Task Admin_update_persists_the_invited_flag_dates_and_head()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var id = ClaimCountryId();
        await CreateCountryAsync(admin, id, "ZD");
        var headId = await SeedDelegateAsync(id);

        var update = await SendAuthAsync(HttpMethod.Put, $"/api/v1/admin/countries/{id}", admin,
            new
            {
                Code = "ZD",
                Name = "Delegation Land",
                NameArabic = "أرض الوفود",
                DisplayOrder = 9990,
                IsActive = true,
                IsInvited = true,
                DelegationArrivalDate = "2026-01-12",
                DelegationDepartureDate = "2026-01-15",
                HeadOfDelegationUserProfileId = headId,
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var detail = await GetCountryAsync(admin, id);
        Assert.True(detail.IsInvited); // the previously-dropped flag now round-trips
        Assert.Equal(new DateOnly(2026, 1, 12), detail.DelegationArrivalDate);
        Assert.Equal(new DateOnly(2026, 1, 15), detail.DelegationDepartureDate);
        Assert.Equal(headId, detail.HeadOfDelegationUserProfileId);
    }

    [Fact]
    public async Task Un_inviting_a_country_clears_its_head_and_dates()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var id = ClaimCountryId();
        await CreateCountryAsync(admin, id, "ZF");
        var headId = await SeedDelegateAsync(id);

        // Invite it with a head + dates...
        await SendAuthAsync(HttpMethod.Put, $"/api/v1/admin/countries/{id}", admin,
            new
            {
                Code = "ZF",
                Name = "Toggle Land",
                NameArabic = "أرض التبديل",
                DisplayOrder = 9992,
                IsActive = true,
                IsInvited = true,
                DelegationArrivalDate = "2026-01-12",
                DelegationDepartureDate = "2026-01-15",
                HeadOfDelegationUserProfileId = headId,
            });

        // ...then un-invite — the head + dates must be cleared (no orphaned data).
        var update = await SendAuthAsync(HttpMethod.Put, $"/api/v1/admin/countries/{id}", admin,
            new
            {
                Code = "ZF",
                Name = "Toggle Land",
                NameArabic = "أرض التبديل",
                DisplayOrder = 9992,
                IsActive = true,
                IsInvited = false,
                DelegationArrivalDate = "2026-01-12",
                DelegationDepartureDate = "2026-01-15",
                HeadOfDelegationUserProfileId = headId,
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var detail = await GetCountryAsync(admin, id);
        Assert.False(detail.IsInvited);
        Assert.Null(detail.HeadOfDelegationUserProfileId);
        Assert.Null(detail.DelegationArrivalDate);
        Assert.Null(detail.DelegationDepartureDate);
    }

    [Fact]
    public async Task Setting_a_non_delegate_as_head_is_a_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var id = ClaimCountryId();
        await CreateCountryAsync(admin, id, "ZE");

        var update = await SendAuthAsync(HttpMethod.Put, $"/api/v1/admin/countries/{id}", admin,
            new
            {
                Code = "ZE",
                Name = "Bad Head",
                NameArabic = "رأس خاطئ",
                DisplayOrder = 9991,
                IsActive = true,
                IsInvited = true,
                HeadOfDelegationUserProfileId = Guid.NewGuid(), // not a delegate of this country
            });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var body = (await update.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.CountryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_lists_only_a_country_active_delegates()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        await EnsureCountryAsync("EG", 818);
        await SeedDelegateAsync(818);
        await SeedDelegateAsync(818, isActive: false); // excluded
        await SeedDelegateAsync(818, isDelegate: false); // excluded

        var response = await SendAuthAsync(
            HttpMethod.Get, "/api/v1/admin/countries/818/delegates", admin, body: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminCountryDelegateOption>>>())!;
        Assert.Single(body.Data!);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>Reads the public delegations view — anonymously by default, or as the
    /// signed-in holder of <paramref name="token"/> (G2 / D-811 per-viewer filter).</summary>
    private async Task<AppDelegations> GetDelegationsAsync(string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/app/delegations");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AppDelegations>>())!.Data!;
    }

    /// <summary>Creates an approved visitor whose profile carries
    /// <paramref name="nationalityId"/> and signs them in — the G2 (D-811) exclusion
    /// keys off exactly that nationality.</summary>
    private async Task<string> CreateVisitorWithNationalityAsync(int nationalityId)
    {
        var email = $"deleg-viewer-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Delegation Viewer",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Delegation Viewer",
                NameArabic = "زائر الوفود",
                NationalityId = nationalityId,
                IsDelegate = true,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private async Task EnsureCountryAsync(string code, int id)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            appDb.Countries.Add(new Country
            {
                Id = id,
                Code = code,
                Name = code,
                NameArabic = code,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }
    }

    private async Task<Guid> SeedDelegateAsync(
        int nationalityId,
        bool isDelegate = true,
        bool isActive = true,
        string name = "Delegate",
        string nameArabic = "مندوب",
        string? jobTitle = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(), // logical FK — no SimfUser needed for counting
            Name = name,
            NameArabic = nameArabic,
            NationalityId = nationalityId,
            IsDelegate = isDelegate,
            IsActive = isActive,
            JobTitle = jobTitle,
            CreatedAt = SimfClock.Now,
        };
        appDb.UserProfiles.Add(profile);
        await appDb.SaveChangesAsync();
        return profile.Id;
    }

    private async Task SetCountryDelegationAsync(
        int countryId,
        bool invited,
        DateOnly? arrival = null,
        DateOnly? departure = null,
        Guid? headProfileId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await appDb.Countries.SingleAsync(c => c.Id == countryId);
        country.IsActive = true;
        country.IsInvited = invited;
        country.DelegationArrivalDate = arrival;
        country.DelegationDepartureDate = departure;
        country.HeadOfDelegationUserProfileId = headProfileId;
        await appDb.SaveChangesAsync();
    }

    private async Task CreateCountryAsync(string adminToken, int id, string code)
    {
        var create = await SendAuthAsync(HttpMethod.Post, "/api/v1/admin/countries", adminToken,
            new AdminCreateCountryRequest
            {
                Id = id,
                Code = code,
                Name = "Seed Country",
                NameArabic = "بلد",
                DisplayOrder = 9000,
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
    }

    private async Task<AdminCountryDetail> GetCountryAsync(string adminToken, int id)
    {
        var response = await SendAuthAsync(
            HttpMethod.Get, $"/api/v1/admin/countries/{id}", adminToken, body: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminCountryDetail>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"deleg-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Delegations Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method, string url, string token, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
