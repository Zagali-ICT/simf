// Tests: B22 — an admin can correct a delegate's nationality.
// UserProfile.NationalityId was written only by the self-service sign-up upsert, so
// nothing in the admin API or the CP could fix a wrong one — yet nationality decides
// which delegation an account belongs to, and therefore whether they may confirm a
// delegation meeting (DelegationMeetingRequestService.ConfirmByOtherPartyAsync /
// DeclineByOtherPartyAsync match on NationalityId). PUT /admin/{visitors,others}/{id}
// now carries an OPTIONAL NationalityCode with the same active-country rule the
// self-service path enforces; omitting it leaves the stored value untouched.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Common;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminAccountNationalityTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminAccountNationalityTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_edit_corrects_a_wrong_nationality()
    {
        var wrong = await EnsureCountryAsync("XR", 9021);
        var right = await EnsureCountryAsync("XS", 9022);
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync(wrong);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Edit Visitor",
                NationalityCode = "xs",   // case-insensitive, like the self-service rule
                AllowsDelegationMeeting = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == id);
        Assert.Equal(right, profile.NationalityId);
        Assert.True(profile.AllowsDelegationMeeting);
    }

    [Fact]
    public async Task Admin_edit_without_a_nationality_leaves_the_stored_one_alone()
    {
        var current = await EnsureCountryAsync("XT", 9023);
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync(current);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = email, DisplayName = "Untouched" },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == id);
        Assert.Equal(current, profile.NationalityId);
    }

    [Fact]
    public async Task An_unknown_nationality_code_is_rejected_and_nothing_is_written()
    {
        var current = await EnsureCountryAsync("XU", 9024);
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync(current);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email, DisplayName = "Should not save", NationalityCode = "ZZ",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileNationalityUnknown, body.Error!.Code);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == id);
        Assert.Equal(current, profile.NationalityId);
        // The whole edit is rejected before any write — the display name is unchanged.
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Equal("Edit Visitor", user.DisplayName);
    }

    [Fact]
    public async Task An_inactive_country_is_rejected()
    {
        var current = await EnsureCountryAsync("XV", 9025);
        await EnsureCountryAsync("XW", 9026, isActive: false);
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync(current);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email, DisplayName = "Edit Visitor", NationalityCode = "XW",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_edit_requires_the_account_management_permission()
    {
        var current = await EnsureCountryAsync("XX", 9027);
        var (id, email) = await CreateVisitorWithProfileAsync(current);
        var visitorToken = await SignInAsync(email);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email, DisplayName = "Self edit", NationalityCode = "XX",
            },
            visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<int> EnsureCountryAsync(string code, int id, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                CreatedAt = SimfClock.Now,
            };
            db.Countries.Add(country);
        }
        country.IsActive = isActive;
        await db.SaveChangesAsync();
        return country.Id;
    }

    private async Task<(Guid Id, string Email)> CreateVisitorWithProfileAsync(int nationalityId)
    {
        var email = $"nat-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Edit Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await db.ProfileTypes.FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
        if (type is null)
        {
            type = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Visitor — NatSeed", NameArabic = "زائر",
                PageColor = "#3B82F6", IsForVisitor = true, IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.ProfileTypes.Add(type);
            await db.SaveChangesAsync();
        }
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = type.Id,
            Name = user.DisplayName, NameArabic = user.DisplayName,
            NationalityId = nationalityId,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return (user.Id, email);
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"nat-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Nat Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<string> SignInAsync(
        string email, SignInAudience audience = SignInAudience.App)
    {
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password, Audience = audience,
            });
        return (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
