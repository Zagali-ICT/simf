// Issue-1 — proves the per-page/per-action permission gate cannot be bypassed
// at the API: a holder of a custom role reaches exactly the endpoints whose
// permission it was granted and is 403'd on the rest, an Administrator reaches
// everything via the wildcard, and a role-less admin reaches nothing.
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
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class PermissionEnforcementTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PermissionEnforcementTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Custom_role_reaches_only_its_granted_endpoint()
    {
        // A custom role granted exactly Sessions.View — nothing else.
        var token = await CreateAdminWithCustomRoleAsync(
            grantedCodes: [PermissionCatalog.Sessions.View]);

        // Granted: the sessions list is reachable (not 403).
        var granted = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        // Not granted: the themes list is forbidden, even though both are
        // "admin" endpoints that used to share the AdministratorOnly gate.
        var denied = await PostAuthAsync("/api/v1/admin/themes/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Administrator_wildcard_reaches_every_admin_endpoint()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var sessions = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        var themes = await PostAuthAsync("/api/v1/admin/themes/list", new GridQuery(), token);

        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, themes.StatusCode);
    }

    [Fact]
    public async Task Admin_user_with_no_permissions_is_forbidden()
    {
        // A custom role with zero grants → an empty `perm` claim set.
        var token = await CreateAdminWithCustomRoleAsync(grantedCodes: []);

        var denied = await PostAuthAsync("/api/v1/admin/sessions/list", new GridQuery(), token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // Creates a UserType.Admin user holding a fresh custom role whose only
    // grants are `grantedCodes`, then signs in on the CP audience and returns
    // the access token. The seeder does not run under the Testing host, so the
    // Permission rows for the granted codes are inserted here.
    private async Task<string> CreateAdminWithCustomRoleAsync(string[] grantedCodes)
    {
        var email = $"perm-{Guid.NewGuid():N}@simf.test";
        var roleName = $"Limited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in grantedCodes)
            {
                var def = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions
                    .SingleOrDefaultAsync(p => p.Code == code);
                if (permission is null)
                {
                    permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Code = def.Code,
                        Page = def.Page,
                        Action = def.Action,
                        DisplayName = def.DisplayName,
                    };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
            await db.SaveChangesAsync();

            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Limited Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await SignInCpAsync(email);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"perm-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Perm Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await SignInCpAsync(email);
    }

    private async Task<string> SignInCpAsync(string email)
    {
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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
}
