// Duplicating an account COPIES its role membership, so duplicating a
// role-holding admin is a role grant wearing a different name.
//
// The duplicate endpoint was gated on Admins.Create alone while the create
// endpoint refused a non-empty Roles list without Admins.AssignRoles. An operator
// holding Create but not AssignRoles therefore could not mint an Administrator
// through the create payload, but could copy an existing Administrator row onto an
// address it controlled and receive the wildcard that way. These tests pin the
// closed door, and pin that it closed on the role grant rather than on duplication
// as such: a role-less source still duplicates on Create alone.
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

[Trait(TestAreas.TraitName, TestAreas.Security)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class DuplicateUserRoleGrantTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DuplicateUserRoleGrantTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Duplicating_an_administrator_without_AssignRoles_is_forbidden_and_creates_nothing()
    {
        // Create, but deliberately NOT AssignRoles - the exact split the create
        // endpoint already enforces on its Roles list.
        var token = await CreateOperatorAsync([PermissionCatalog.Admins.Create]);
        var sourceId = await CreateAdminAsync(AdministratorRole);
        var newEmail = $"escalate-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = newEmail },
            token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Refused BEFORE the account is created, so the attempt leaves nothing
        // behind. A 403 that had still minted the elevated copy would be worse than
        // no gate at all, because the response would say the escalation failed.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        Assert.Null(await users.FindByEmailAsync(newEmail));
    }

    [Fact]
    public async Task Duplicating_an_administrator_with_AssignRoles_still_copies_the_roles()
    {
        // The positive control. Without it the test above would pass just as
        // happily against a duplicate endpoint broken for everyone, hiding a live
        // regression rather than proving a gate.
        var token = await CreateOperatorAsync(
            [PermissionCatalog.Admins.Create, PermissionCatalog.Admins.AssignRoles]);
        var sourceId = await CreateAdminAsync(AdministratorRole);
        var newEmail = $"authorised-copy-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = newEmail },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var copy = await users.FindByEmailAsync(newEmail);
        Assert.NotNull(copy);
        Assert.True(await users.IsInRoleAsync(copy!, AdministratorRole));
    }

    [Fact]
    public async Task Duplicating_a_role_less_admin_still_needs_only_Create()
    {
        // The gate closes on the ROLE GRANT, not on duplication. A source holding
        // no roles grants nothing, so Create alone stays enough - otherwise the fix
        // would have broken the ordinary duplicate button for every operator who is
        // not also a role administrator.
        var token = await CreateOperatorAsync([PermissionCatalog.Admins.Create]);
        var sourceId = await CreateAdminAsync(roleName: null);
        var newEmail = $"plain-copy-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = newEmail },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        Assert.NotNull(await users.FindByEmailAsync(newEmail));
    }

    // -- fixture ---------------------------------------------------------------

    /// <summary>An approved Admin account, optionally holding
    /// <paramref name="roleName"/>. Returns its user id, which is what a duplicate
    /// request names as its source.</summary>
    private async Task<Guid> CreateAdminAsync(string? roleName)
    {
        var email = $"dup-source-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        if (roleName is not null && !await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new SimfRole { Name = roleName, IsBaseline = true });
        }

        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Duplicate Source",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        var created = await users.CreateAsync(user, AuthFlow.Password);
        Assert.True(created.Succeeded,
            "Could not seed the duplicate source: "
            + string.Join("; ", created.Errors.Select(error => error.Description)));

        if (roleName is not null)
        {
            await users.AddToRoleAsync(user, roleName);
        }

        return user.Id;
    }

    // Mirrors PermissionEnforcementTests: a UserType.Admin holding a fresh custom
    // role whose only grants are `grantedCodes`. The seeder does not run under the
    // Testing host, so the Permission rows are inserted here.
    private async Task<string> CreateOperatorAsync(string[] grantedCodes)
    {
        var email = $"dup-operator-{Guid.NewGuid():N}@simf.test";
        var roleName = $"DupLimited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in grantedCodes)
            {
                var definition = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions.SingleOrDefaultAsync(row => row.Code == code);
                if (permission is null)
                {
                    permission = new Permission { Id = Guid.NewGuid(), Code = definition.Code };
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
                DisplayName = "Duplicate Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
