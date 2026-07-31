// D-161 — ProfileType.MobileAppRole + JWT mobile_app_role claim.
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MobileAppRoleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MobileAppRoleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProfileType_create_and_get_round_trips_MobileAppRole_value()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var name = $"Programme Coordinator {Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "منسّق برنامج",
                PageColor = "#244A77",
                MobileAppRole = "Moderator",
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Moderator", created.MobileAppRole);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{created.Id}", token);
        var got = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Moderator", got.MobileAppRole);
    }

    [Fact]
    public async Task ProfileType_create_with_no_MobileAppRole_defaults_to_None()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var name = $"Exhibitor {Guid.NewGuid():N}";
        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "عارض",
                PageColor = "#A78BFA",
                // MobileAppRole omitted → defaults to None
            },
            token);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("None", detail.MobileAppRole);
    }

    [Fact]
    public async Task ProfileType_create_with_Visitor_MobileAppRole_is_rejected_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
                UserType = "Visitor",
                IsVisitor = false,
                Name = $"Invalid {Guid.NewGuid():N}",
                NameArabic = "غير صالح",
                PageColor = "#FF0000",
                MobileAppRole = "Visitor", // resolved from UserType, never set per ProfileType
            },
            token);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfileType_update_changes_MobileAppRole_value()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
                UserType = "Visitor",
                IsVisitor = false,
                Name = $"Volunteer {Guid.NewGuid():N}",
                NameArabic = "متطوّع",
                PageColor = "#22D3EE",
                MobileAppRole = "Staff",
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Staff", created.MobileAppRole);

        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{created.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = created.Name,
                NameArabic = created.NameArabic,
                PageColor = created.PageColor,
                MobileAppRole = "Moderator",
                IsActive = true,
            },
            token);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Moderator", updated.MobileAppRole);
    }

    [Fact]
    public async Task Visitor_signin_JWT_carries_mobile_app_role_Visitor()
    {
        var (visitorToken, _) = await CreateVisitorAndSignInAsync();
        var claim = ReadClaim(visitorToken, "mobile_app_role");
        Assert.Equal("Visitor", claim);
    }

    [Fact]
    public async Task Admin_signin_JWT_carries_mobile_app_role_None()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var claim = ReadClaim(adminToken, "mobile_app_role");
        Assert.Equal("None", claim);
    }

    // D-194 — a self-registering user who self-picks a partner (Staff/
    // Moderator) profile type stays PendingApproval, so the resolver must
    // hold them at Visitor until an admin approves. This is the regression
    // guard for the mobile sign-up privilege-escalation finding.
    [Fact]
    public async Task PendingApproval_with_partner_Staff_profile_resolves_to_Visitor()
    {
        var userId = await SeedVisitorWithPartnerProfileAsync(
            MobileAppRole.Staff, AccountState.PendingApproval);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
        var resolved = await service.ResolveMobileAppRoleAsync(userId);

        Assert.Equal(MobileAppRole.Visitor, resolved);
    }

    // D-194 — once an admin has approved the account (the blessing of the
    // proposed profile type), the partner role applies. Proves the gate
    // does not over-correct and break legitimately admin-approved staff.
    [Fact]
    public async Task Approved_with_partner_Staff_profile_resolves_to_Staff()
    {
        var userId = await SeedVisitorWithPartnerProfileAsync(
            MobileAppRole.Staff, AccountState.Approved);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
        var resolved = await service.ResolveMobileAppRoleAsync(userId);

        Assert.Equal(MobileAppRole.Staff, resolved);
    }

    // D-519 — an Approved user on a partner (IsVisitor=false) profile type whose
    // MobileAppRole is Exhibitor resolves to the new Exhibitor app role (not the
    // None→Visitor floor). Proves the resolver returns the additive value.
    [Fact]
    public async Task Approved_with_partner_Exhibitor_profile_resolves_to_Exhibitor()
    {
        var userId = await SeedVisitorWithPartnerProfileAsync(
            MobileAppRole.Exhibitor, AccountState.Approved);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
        var resolved = await service.ResolveMobileAppRoleAsync(userId);

        Assert.Equal(MobileAppRole.Exhibitor, resolved);
    }

    // D-519 — the CP can now assign the Exhibitor role: the admin ProfileType
    // create accepts "Exhibitor" (ParseMobileAppRole no longer rejects it) and it
    // round-trips, so editing the seeded Exhibitor row no longer downgrades it.
    [Fact]
    public async Task ProfileType_create_with_Exhibitor_MobileAppRole_round_trips()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = $"Exhibitor {Guid.NewGuid():N}",
                NameArabic = "عارض",
                PageColor = "#0891B2",
                MobileAppRole = "Exhibitor",
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Exhibitor", created.MobileAppRole);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{created.Id}", token);
        var got = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal("Exhibitor", got.MobileAppRole);
    }

    // -- Helpers --------------------------------------------------------------

    // Seeds a Visitor user with a partner (IsVisitor=false) profile type
    // carrying the given MobileAppRole, at the given account state. Mirrors
    // the SeedVisitorProfileAsync pattern in AdminInvitationsTests.
    private async Task<Guid> SeedVisitorWithPartnerProfileAsync(
        MobileAppRole role, AccountState state)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Partner {Guid.NewGuid():N}",
            NameArabic = "شريك",
            PageColor = "#244A77",
            IsForVisitor = false,
            MobileAppRole = role,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(profileType);
        await appDb.SaveChangesAsync();

        var email = $"mar-partner-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Partner User",
            AccountState = state,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileType.Id,
            Name = "Partner User",
            NameArabic = "مستخدم شريك",
            PlaceOfBirth = "Riyadh",
            NationalityId = 0,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();

        return user.Id;
    }

    private static string? ReadClaim(string token, string claimType)
    {
        var jwt = new JwtSecurityToken(token);
        return jwt.Claims.SingleOrDefault(c => c.Type == claimType)?.Value;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"mar-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Mobile-Role Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<(string Token, Guid UserId)> CreateVisitorAndSignInAsync()
    {
        var email = $"mar-visitor-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Mobile-Role Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.App,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
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

    private async Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
