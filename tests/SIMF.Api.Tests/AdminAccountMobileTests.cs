// Tests: FR-PHN-002 — an admin can correct an existing account's mobile number.
// Every admin surface showed UserProfile.SaudiMobile / InternationalMobile READ-ONLY
// and only the walk-in CREATE desk had an editable field, so a number typed wrong at
// registration could never be fixed — the account stayed unreachable. PUT
// /admin/{visitors,others}/{id} now carries OPTIONAL SaudiMobile / InternationalMobile
// validated by the SAME shapes as the self-service upsert and the walk-in desk;
// omitting them leaves the stored numbers untouched, and the stored value is the
// canonical form (DEF-PHN-003). Gated by the existing Visitors.Edit / Others.Edit
// account-management permission — no new permission, this extends an existing action.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminAccountMobileTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminAccountMobileTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_edit_corrects_a_wrong_Saudi_mobile()
    {
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Edit Visitor",
                SaudiMobile = "0559876543",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("0559876543", (await LoadProfileAsync(id)).SaudiMobile);
    }

    [Fact]
    public async Task Admin_edit_stores_the_mobile_canonicalised()
    {
        // DEF-PHN-003 — a desk-typed "+966-55 598 7654" must land in the column as
        // the same string the app would have written for that number.
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Edit Visitor",
                SaudiMobile = "+966-55 598 7654",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("+966555987654", (await LoadProfileAsync(id)).SaudiMobile);
    }

    [Fact]
    public async Task Admin_edit_sets_the_international_mobile()
    {
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Edit Visitor",
                InternationalMobile = "0044-7700 900123",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await LoadProfileAsync(id);
        Assert.Equal("+447700900123", profile.InternationalMobile);
        // The Saudi number was not sent, so it is untouched.
        Assert.Equal("0501111111", profile.SaudiMobile);
    }

    [Fact]
    public async Task Admin_edit_without_a_mobile_leaves_the_stored_one_alone()
    {
        // The "null = no change" contract: a desk fixing only the display name
        // must not wipe the contact detail.
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = email, DisplayName = "Untouched" },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("0501111111", (await LoadProfileAsync(id)).SaudiMobile);
    }

    [Fact]
    public async Task A_malformed_Saudi_mobile_is_rejected_and_nothing_is_written()
    {
        // The admin path validates IDENTICALLY to the self-service rule
        // (0401234567 is not the 05 mobile plan).
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Should not save",
                SaudiMobile = "0401234567",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal("0501111111", (await LoadProfileAsync(id)).SaudiMobile);
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await identity.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Equal("Edit Visitor", user.DisplayName);
    }

    [Fact]
    public async Task A_malformed_international_mobile_is_rejected()
    {
        var token = await CreateAdminAndSignInAsync();
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Edit Visitor",
                InternationalMobile = "+44",   // too short for E.164
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_mobile_edit_requires_the_account_management_permission()
    {
        var (id, email) = await CreateVisitorWithProfileAsync("0501111111");
        var visitorToken = await SignInAsync(email);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = email, DisplayName = "Self edit", SaudiMobile = "0559876543",
            },
            visitorToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<UserProfile> LoadProfileAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
    }

    private async Task<(Guid Id, string Email)> CreateVisitorWithProfileAsync(
        string saudiMobile)
    {
        var email = $"mob-visitor-{Guid.NewGuid():N}@simf.test";
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
                Name = "Visitor — MobSeed", NameArabic = "زائر",
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
            SaudiMobile = saudiMobile,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return (user.Id, email);
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"mob-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Mob Admin",
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
