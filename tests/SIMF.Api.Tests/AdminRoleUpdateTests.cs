// Tests: SIMF.Api/Endpoints/Admin/RoleEndpoints.cs (UpdateRoleEndpoint)
//
// D-844 — `PUT /api/v1/admin/roles/{id}` had NO test of any kind. RoleEndpoints.cs
// carried `// Tests: SIMF.Api.Tests/AdminRolesTests.cs`, a file that does not exist
// anywhere in the repository, so the traceability header pointed at nothing and the
// rename path shipped uncovered.
//
// That was discovered while converting the endpoint's route DTO to the D-505
// inheriting shape. Every other conversion in this programme was proved by
// reverting it and watching a named test fail; this one had nothing to fail, so the
// coverage is written first and the conversion is only then verifiable.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminRoleUpdateTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminRoleUpdateTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_role_can_be_renamed_and_the_new_name_is_readable_afterwards()
    {
        // The whole payload of this endpoint is one field. If Name ever stops
        // arriving — the failure mode D-842/D-843 hit three times on other
        // endpoints — the rename silently becomes a no-op, or worse writes an
        // empty name. Asserted by re-reading the row, not by trusting the
        // response the service composed in memory.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var original = $"Renameable {Guid.NewGuid():N}";
        var roleId = await CreateRoleAsync(adminToken, original);

        var renamed = $"Renamed {Guid.NewGuid():N}";
        using var response = await PutAuthAsync(
            $"/api/v1/admin/roles/{roleId}",
            new AdminUpdateRoleRequest { Name = renamed },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRoleSummary>>())!.Data!;
        Assert.Equal(renamed, summary.Name);

        var listed = await FindRoleAsync(adminToken, roleId);
        Assert.NotNull(listed);
        Assert.Equal(renamed, listed!.Name);
    }

    [Fact]
    public async Task Renaming_a_baseline_role_is_refused_with_409()
    {
        // AdminRoleService guards the seeded roles, and Administrator is the one
        // whose loss would lock every admin out of the Control Panel.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var administrator = await FindRoleByNameAsync(adminToken, AdministratorRole);
        Assert.NotNull(administrator);

        using var response = await PutAuthAsync(
            $"/api/v1/admin/roles/{administrator!.Id}",
            new AdminUpdateRoleRequest { Name = "Administrator Renamed" },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RoleIsBaseline, body.Error!.Code);

        // ...and the guard actually protected the row, rather than only shaping
        // the response.
        var stillThere = await FindRoleByNameAsync(adminToken, AdministratorRole);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task A_blank_name_is_refused_with_400()
    {
        // The service trims before length-checking, so whitespace must not slip
        // through and blank out a role's name.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var roleId = await CreateRoleAsync(adminToken, $"Blankable {Guid.NewGuid():N}");

        using var response = await PutAuthAsync(
            $"/api/v1/admin/roles/{roleId}",
            new AdminUpdateRoleRequest { Name = "   " },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateRoleAsync(string token, string name)
    {
        using var response = await PostAuthAsync(
            "/api/v1/admin/roles", new AdminCreateRoleRequest { Name = name }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminRoleSummary>>())!.Data!;
        return summary.Id;
    }

    private async Task<AdminRoleSummary?> FindRoleAsync(string token, Guid id)
    {
        var rows = await ListRolesAsync(token);
        return rows.FirstOrDefault(row => row.Id == id);
    }

    private async Task<AdminRoleSummary?> FindRoleByNameAsync(string token, string name)
    {
        var rows = await ListRolesAsync(token);
        return rows.FirstOrDefault(
            row => string.Equals(row.Name, name, StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<AdminRoleSummary>> ListRolesAsync(string token)
    {
        using var response = await PostAuthAsync(
            "/api/v1/admin/roles/list", new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminRoleSummary>>>())!;
        return body.Data!.Items;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"role-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Role Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class =>
        SendAuthAsync(HttpMethod.Post, url, body, token);

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class =>
        SendAuthAsync(HttpMethod.Put, url, body, token);

    private Task<HttpResponseMessage> SendAuthAsync<TBody>(
        HttpMethod method, string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
