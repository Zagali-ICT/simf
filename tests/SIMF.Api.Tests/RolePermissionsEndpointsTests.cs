// Issue-1 / Issue-3 — end-to-end coverage of the role→permission config surface
// (GET/PUT /admin/roles/{id}/permissions) that the CP RolePermissionsEditor
// drives. Confirms the grant set round-trips, PUT replaces (not appends),
// baseline roles are refused, and unknown codes are rejected.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class RolePermissionsEndpointsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public RolePermissionsEndpointsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Put_then_get_round_trips_and_replaces_the_grant_set()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var roleId = await CreateCustomRoleAsync(token);

        // PUT an initial set, GET it back.
        await PutPermissionsAsync(roleId,
            [PermissionCatalog.Sessions.View, PermissionCatalog.Themes.Edit], token);
        var first = await GetPermissionsAsync(roleId, token);
        Assert.False(first.IsBaseline);
        Assert.Equal(
            new[] { PermissionCatalog.Sessions.View, PermissionCatalog.Themes.Edit }.OrderBy(c => c),
            first.GrantedCodes.OrderBy(c => c));

        // PUT a different set — it must REPLACE, not append (diff-apply).
        await PutPermissionsAsync(roleId,
            [PermissionCatalog.Themes.Edit, PermissionCatalog.Halls.View], token);
        var second = await GetPermissionsAsync(roleId, token);
        Assert.Equal(
            new[] { PermissionCatalog.Themes.Edit, PermissionCatalog.Halls.View }.OrderBy(c => c),
            second.GrantedCodes.OrderBy(c => c));
        Assert.DoesNotContain(PermissionCatalog.Sessions.View, second.GrantedCodes);
    }

    [Fact]
    public async Task Put_on_a_baseline_role_is_refused()
    {
        var token = await CreateAdministratorAndSignInAsync();

        // Resolve the seeded Administrator role id.
        Guid baselineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var role = await roleManager.FindByNameAsync(AdministratorRole);
            baselineId = role!.Id;
        }

        var response = await PutRawAsync(baselineId,
            new AdminSetRolePermissionsRequest { Codes = [PermissionCatalog.Sessions.View] }, token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_an_unknown_code_is_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var roleId = await CreateCustomRoleAsync(token);

        var response = await PutRawAsync(roleId,
            new AdminSetRolePermissionsRequest { Codes = ["Bogus.NotACode"] }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_rolls_the_security_stamp_of_every_holder_of_the_role()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var roleId = await CreateCustomRoleAsync(token);

        // Permission codes are baked into the access token, so editing the grants
        // without rolling the holder's stamp leaves a removed permission live in
        // every token already issued. The stamp is the only revocation channel.
        var holderEmail = $"roleperm-holder-{Guid.NewGuid():N}@simf.test";
        string? before;
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var role = await roleManager.FindByIdAsync(roleId.ToString());
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var holder = new SimfUser
            {
                UserName = holderEmail,
                Email = holderEmail,
                EmailConfirmed = true,
                DisplayName = "RolePerm Holder",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(holder, AuthFlow.Password);
            await users.AddToRoleAsync(holder, role!.Name!);
            before = (await users.FindByEmailAsync(holderEmail))!.SecurityStamp;
        }

        await PutPermissionsAsync(roleId, [PermissionCatalog.Sessions.View], token);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var after = (await users.FindByEmailAsync(holderEmail))!.SecurityStamp;
            Assert.NotEqual(before, after);
        }
    }

    // -- helpers ------------------------------------------------------------

    private async Task<Guid> CreateCustomRoleAsync(string token)
    {
        var response = await PostAuthAsync("/api/v1/admin/roles",
            new AdminCreateRoleRequest { Name = $"E2E-{Guid.NewGuid():N}" }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ApiResult<AdminRoleSummary>>())!.Data!;
        return summary.Id;
    }

    private async Task PutPermissionsAsync(Guid roleId, string[] codes, string token)
    {
        var response = await PutRawAsync(roleId,
            new AdminSetRolePermissionsRequest { Codes = codes.ToList() }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PutRawAsync(
        Guid roleId, AdminSetRolePermissionsRequest body, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/admin/roles/{roleId}/permissions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<AdminRolePermissionsResponse> GetPermissionsAsync(Guid roleId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/roles/{roleId}/permissions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminRolePermissionsResponse>>())!.Data!;
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

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"roleperm-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "RolePerm Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
