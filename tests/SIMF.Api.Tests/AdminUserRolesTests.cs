// Issue-1 — coverage of the existing-user role editor (GET/PUT
// /admin/admins/{id}/roles) that replaced the stubbed CP Edit dialog.
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

public sealed class AdminUserRolesTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUserRolesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Put_then_get_round_trips_and_replaces_a_users_roles()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var targetId = await CreateAdminUserAsync();
        var roleName = await CreateCustomRoleAsync(token);

        // Assign the custom role, read it back.
        var put1 = await PutRolesAsync(targetId, [roleName], token);
        Assert.Equal(HttpStatusCode.OK, put1.StatusCode);
        var after1 = await GetRolesAsync(targetId, token);
        Assert.Contains(roleName, after1.Roles);

        // Clear the roles — PUT replaces (the set becomes empty).
        var put2 = await PutRolesAsync(targetId, [], token);
        Assert.Equal(HttpStatusCode.OK, put2.StatusCode);
        var after2 = await GetRolesAsync(targetId, token);
        Assert.DoesNotContain(roleName, after2.Roles);
    }

    [Fact]
    public async Task Put_is_rejected_for_a_non_admin_user()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var visitorId = await CreateVisitorUserAsync();

        var response = await PutRolesAsync(visitorId, [AdministratorRole], token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_an_unknown_role_is_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var targetId = await CreateAdminUserAsync();

        var response = await PutRolesAsync(targetId, ["No.Such.Role"], token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- helpers ------------------------------------------------------------

    private async Task<string> CreateCustomRoleAsync(string token)
    {
        var name = $"UR-{Guid.NewGuid():N}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/roles")
        {
            Content = JsonContent.Create(new AdminCreateRoleRequest { Name = name }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ApiResult<AdminRoleSummary>>())!.Data!;
        return summary.Name; // the PUT body assigns roles by name
    }

    private Task<HttpResponseMessage> PutRolesAsync(Guid userId, string[] roles, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/admin/admins/{userId}/roles")
        {
            Content = JsonContent.Create(new AdminSetUserRolesRequest { Roles = roles.ToList() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<AdminUserRolesResponse> GetRolesAsync(Guid userId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/admins/{userId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminUserRolesResponse>>())!.Data!;
    }

    private async Task<Guid> CreateAdminUserAsync() =>
        await CreateUserAsync(UserType.Admin, "Target Admin");

    private async Task<Guid> CreateVisitorUserAsync() =>
        await CreateUserAsync(UserType.Visitor, "Target Visitor");

    private async Task<Guid> CreateUserAsync(UserType userType, string displayName)
    {
        var email = $"ur-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            AccountState = AccountState.Approved,
            UserType = userType,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"ur-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "UR Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
