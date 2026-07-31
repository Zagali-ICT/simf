// D-728 (owner item 9) — POST /api/v1/admin/accounts/{id}/change-type.
// Flips an account between the audience (Visitor) and partner (Other) scope by
// reassigning its ProfileType to one in the OPPOSITE scope. The flip rolls the
// security stamp (privilege change) and leaves the approval state unchanged.
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

public sealed class AdminChangeAccountTypeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminChangeAccountTypeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Change_type_flips_a_visitor_to_a_partner_type()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null); // audience (Visitor)
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = partnerTypeId },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var assigned = await appDb.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ProfileTypeId)
            .SingleAsync();
        Assert.Equal(partnerTypeId, assigned);
    }

    [Fact]
    public async Task Change_type_flips_a_partner_account_back_to_a_visitor_type()
    {
        var token = await CreateAdminAndSignInAsync();
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);
        var userId = await CreateAccountAsync(profileTypeId: partnerTypeId); // partner (Other)
        var visitorTypeId = await SeedProfileTypeAsync(isForVisitor: true);

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = visitorTypeId },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var assigned = await appDb.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ProfileTypeId)
            .SingleAsync();
        Assert.Equal(visitorTypeId, assigned);
    }

    [Fact]
    public async Task Change_type_rolls_the_security_stamp()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null);
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);

        string stampBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            stampBefore = (await db.Users.AsNoTracking()
                .SingleAsync(u => u.Id == userId)).SecurityStamp!;
        }

        await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = partnerTypeId },
            token);

        using var after = _factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var stampAfter = (await db2.Users.AsNoTracking()
            .SingleAsync(u => u.Id == userId)).SecurityStamp!;
        Assert.NotEqual(stampBefore, stampAfter);
    }

    [Fact]
    public async Task Change_type_keeps_the_approval_state()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null); // Approved
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);

        await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = partnerTypeId },
            token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var state = (await db.Users.AsNoTracking()
            .SingleAsync(u => u.Id == userId)).AccountState;
        Assert.Equal(AccountState.Approved, state);
    }

    [Fact]
    public async Task Change_type_to_a_same_scope_type_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null); // audience (Visitor)
        var sameScopeTypeId = await SeedProfileTypeAsync(isForVisitor: true); // still visitor

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = sameScopeTypeId },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_type_to_an_inactive_type_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null);
        var inactiveTypeId = await SeedProfileTypeAsync(isForVisitor: false, isActive: false);

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = inactiveTypeId },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_type_with_empty_target_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var userId = await CreateAccountAsync(profileTypeId: null);

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{userId}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = Guid.Empty },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_type_for_unknown_id_is_404()
    {
        var token = await CreateAdminAndSignInAsync();
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);

        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{Guid.NewGuid()}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = partnerTypeId },
            token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Change_type_by_a_non_admin_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            $"/api/v1/admin/accounts/{Guid.NewGuid()}/change-type",
            new AdminChangeAccountTypeRequest { NewProfileTypeId = Guid.NewGuid() },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedProfileTypeAsync(bool isForVisitor, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Type {Guid.NewGuid():N}",
            NameArabic = "نوع",
            PageColor = "#244A77",
            IsForVisitor = isForVisitor,
            MobileAppRole = isForVisitor ? MobileAppRole.None : MobileAppRole.Staff,
            IsActive = isActive,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(type);
        await appDb.SaveChangesAsync();
        return type.Id;
    }

    // Creates an Approved Visitor-typed SimfUser and, when profileTypeId is
    // supplied, a UserProfile linking it (that makes the account partner-scoped
    // when the type is IsForVisitor=false). A null profileTypeId leaves the
    // account audience-scoped with no profile row.
    private async Task<Guid> CreateAccountAsync(Guid? profileTypeId)
    {
        var email = $"ct-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Change-Type Subject",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        if (profileTypeId is not null)
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProfileTypeId = profileTypeId,
                Name = "Change-Type Subject",
                NameArabic = "الحساب",
                PlaceOfBirth = "Riyadh",
                NationalityId = 0,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }
        return user.Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"ct-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Change-Type Admin",
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

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
