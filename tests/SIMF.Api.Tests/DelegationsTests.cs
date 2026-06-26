// Tests: D-499 (الوفود, Figma 1426:10771) — the public delegations view
// (GET /app/delegations) + the CP head-of-delegation / dates on the country form
// (PublicDelegationService, AdminCountryService, CountryEndpoints).
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
/// sets the dates + head (an active delegate of that country).
/// </summary>
public sealed class DelegationsTests : IClassFixture<SimfApiFactory>
{
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
        // Saudi Arabia, invited, with a designated head + two more delegates.
        await EnsureCountryAsync("SA", 682);
        var headId = await SeedDelegateAsync(
            682,
            name: "Prince Abdulaziz",
            nameArabic: "سمو الأمير عبدالعزيز",
            jobTitle: "نائب وزير الدفاع");
        await SeedDelegateAsync(682);
        await SeedDelegateAsync(682);
        await SetCountryDelegationAsync(
            682,
            invited: true,
            arrival: new DateOnly(2026, 1, 12),
            departure: new DateOnly(2026, 1, 15),
            headProfileId: headId);

        var data = await GetDelegationsAsync();
        var item = Assert.Single(data.Items, i => i.CountryId == 682);
        Assert.Equal(3, item.MemberCount);
        Assert.Equal("Prince Abdulaziz", item.HeadName);
        Assert.Equal("سمو الأمير عبدالعزيز", item.HeadNameArabic);
        Assert.Equal("نائب وزير الدفاع", item.HeadTitle);
        Assert.Equal(new DateOnly(2026, 1, 12), item.ArrivalDate);
        Assert.Equal(new DateOnly(2026, 1, 15), item.DepartureDate);
        Assert.Equal("SA", item.CountryCode);
        Assert.True(data.CountryCount >= 1);
        Assert.True(data.TotalParticipants >= 3);
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
        var id = Random.Shared.Next(900, 989);
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
        var id = Random.Shared.Next(900, 989);
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
        var id = Random.Shared.Next(900, 989);
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

    private async Task<AppDelegations> GetDelegationsAsync()
    {
        var response = await _client.GetAsync("/api/v1/app/delegations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AppDelegations>>())!.Data!;
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
                CreatedAt = DateTimeOffset.UtcNow,
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
            CreatedAt = DateTimeOffset.UtcNow,
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
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
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
