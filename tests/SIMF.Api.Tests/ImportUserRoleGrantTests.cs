// A workbook row whose Role cell reads "Administrator" grants the Administrator
// role, and that role IS the "*" wildcard - so importing one is a role assignment
// wearing a spreadsheet's clothes.
//
// The import endpoint is gated on Admins.Import alone, while the create endpoint
// refuses a non-empty Roles list without Admins.AssignRoles. An operator holding
// Import but not AssignRoles could therefore upload a one-row workbook naming an
// address they control, flag it Administrator, and receive the wildcard through a
// door the role-assignment gate never sees. These tests pin the closed door, and
// pin that it closed on the ROLE GRANT rather than on importing as such: a plain
// row still imports on Import alone, and the elevation row is refused per row so
// the rest of a legitimate workbook still lands.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
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
public sealed class ImportUserRoleGrantTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ImportUserRoleGrantTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Importing_an_Administrator_row_without_AssignRoles_creates_nothing()
    {
        // Import, but deliberately NOT AssignRoles - the exact split the create
        // endpoint already enforces on its Roles list.
        var token = await CreateOperatorAsync([PermissionCatalog.Admins.Import]);
        var escalated = $"import-escalate-{Guid.NewGuid():N}@simf.test";

        var response = await ImportAsync(token, [(escalated, "Escalation Attempt", "Administrator")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>())!;
        Assert.Equal(0, body.Data!.Created);
        Assert.Equal(1, body.Data.Skipped);
        Assert.Contains(body.Data.Errors, error => error.Email == escalated);

        // Refused BEFORE the account is created, so the attempt leaves nothing
        // behind - not even an un-roled account holding a live invitation code.
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        Assert.Null(await users.FindByEmailAsync(escalated));
    }

    [Fact]
    public async Task Importing_an_Administrator_row_with_AssignRoles_still_grants_the_role()
    {
        // The positive control. Without it the test above would pass just as
        // happily against an import broken for everyone.
        var token = await CreateOperatorAsync(
            [PermissionCatalog.Admins.Import, PermissionCatalog.Admins.AssignRoles]);
        var authorised = $"import-authorised-{Guid.NewGuid():N}@simf.test";

        var response = await ImportAsync(token, [(authorised, "Authorised Admin", "Administrator")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>())!;
        Assert.Equal(1, body.Data!.Created);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var created = await users.FindByEmailAsync(authorised);
        Assert.NotNull(created);
        Assert.True(await users.IsInRoleAsync(created!, AdministratorRole));
    }

    [Fact]
    public async Task A_plain_row_still_imports_on_Import_alone_and_the_elevation_row_is_skipped()
    {
        // The gate closes on the ROLE GRANT, not on importing. A mixed workbook
        // lands its ordinary rows and refuses only the flagged one, so the fix does
        // not break the import button for every operator who is not also a role
        // administrator.
        var token = await CreateOperatorAsync([PermissionCatalog.Admins.Import]);
        var plain = $"import-plain-{Guid.NewGuid():N}@simf.test";
        var escalated = $"import-mixed-{Guid.NewGuid():N}@simf.test";

        var response = await ImportAsync(token,
        [
            (plain, "Plain Import", string.Empty),
            (escalated, "Mixed Escalation", "Administrator"),
        ]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>())!;
        Assert.Equal(1, body.Data!.Created);
        Assert.Equal(1, body.Data.Skipped);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        Assert.NotNull(await users.FindByEmailAsync(plain));
        Assert.Null(await users.FindByEmailAsync(escalated));
    }

    // -- fixture ---------------------------------------------------------------

    /// <summary>Uploads a one-sheet workbook to the Admin-family import endpoint.
    /// Column 4 is the Role cell the parser reads as the Administrator flag.</summary>
    private async Task<HttpResponseMessage> ImportAsync(
        string token, (string Email, string DisplayName, string Role)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 4).Value = "Role";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Email;
            sheet.Cell(i + 2, 2).Value = rows[i].DisplayName;
            sheet.Cell(i + 2, 4).Value = rows[i].Role;
        }
        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(buffer.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "import.xlsx");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/admins/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    // Mirrors DuplicateUserRoleGrantTests: a UserType.Admin holding a fresh custom
    // role whose only grants are `grantedCodes`. The seeder does not run under the
    // Testing host, so the Permission rows are inserted here.
    private async Task<string> CreateOperatorAsync(string[] grantedCodes)
    {
        var email = $"import-operator-{Guid.NewGuid():N}@simf.test";
        var roleName = $"ImportLimited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(
                    new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }

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
                DisplayName = "Import Operator",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
