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

/// <summary>
/// D-113 integration tests for the type-scoped visitor grid surface —
/// bulk-delete, duplicate, export, import on <c>/admin/visitors/*</c>.
/// Confirms the type-smuggling guards (admin / other ids must NOT be
/// touched by the visitor endpoints) and the optional <c>ProfileTypeId</c>
/// import column.
/// </summary>
public sealed class AdminGridVisitorsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminGridVisitorsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- Bulk delete ----------------------------------------------------------

    [Fact]
    public async Task Visitor_bulk_delete_succeeds_and_skips_non_visitor_ids()
    {
        var (adminToken, _) = await CreateAdminAsync();

        var visitor = await CreateVisitorAsync(adminToken);
        var adminVictim = await CreateAdminVictimAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { visitor, adminVictim, Guid.NewGuid() },
                Reason = "Visitor bulk-delete test — admin and unknown ids must be skipped.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(1, body.Data!.Deleted);
        Assert.Equal(2, body.Data.Skipped);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var visitorRow = await db.Users.SingleAsync(u => u.Id == visitor);
        Assert.Equal(AccountState.Disabled, visitorRow.AccountState);

        // The Admin-typed user fed into the visitor endpoint must still be
        // active — the type-smuggling guard does not let cross-type bulk
        // operations touch the wrong family.
        var adminRow = await db.Users.SingleAsync(u => u.Id == adminVictim);
        Assert.NotEqual(AccountState.Disabled, adminRow.AccountState);
    }

    // -- Duplicate ------------------------------------------------------------

    [Fact]
    public async Task Visitor_duplicate_creates_a_visitor_copy()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var source = await CreateVisitorAsync(adminToken, displayName: "Source Visitor");
        var newEmail = $"v-dup-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/duplicate",
            new AdminDuplicateUserRequest { SourceId = source, NewEmail = newEmail },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var copy = await users.FindByEmailAsync(newEmail);
        Assert.NotNull(copy);
        Assert.Equal(UserType.Visitor, copy!.UserType);
    }

    [Fact]
    public async Task Visitor_duplicate_of_an_admin_source_returns_404()
    {
        var (adminToken, _) = await CreateAdminAsync();
        // Source is an Admin — the visitor endpoint must refuse.
        var adminSource = await CreateAdminVictimAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/duplicate",
            new AdminDuplicateUserRequest
            {
                SourceId = adminSource,
                NewEmail = $"v-dup-fail-{Guid.NewGuid():N}@simf.test",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminUserNotFound, body.Error!.Code);
    }

    // -- Export ---------------------------------------------------------------

    [Fact]
    public async Task Visitor_export_filters_out_non_visitor_ids()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var visitor = await CreateVisitorAsync(adminToken, displayName: "Export Visitor");
        var adminVictim = await CreateAdminVictimAsync(adminToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/visitors/export")
        {
            Content = JsonContent.Create(
                new AdminExportUsersRequest
                {
                    Ids = new List<Guid> { visitor, adminVictim },
                }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Users");
        // Row 2 = first data row. The admin id must NOT appear.
        Assert.Equal("Export Visitor", sheet.Cell(2, 2).GetString());
        Assert.True(string.IsNullOrEmpty(sheet.Cell(3, 1).GetString()),
            "The admin-victim row must not appear in a visitor export.");
    }

    // -- Import ---------------------------------------------------------------

    [Fact]
    public async Task Visitor_import_creates_visitors_and_ignores_smuggled_admin_role()
    {
        var (adminToken, _) = await CreateAdminAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 4).Value = "Role";
        var marker = $"vimp{Guid.NewGuid():N}".Substring(0, 9);
        var e1 = $"{marker}-1@simf.test";
        var e2 = $"{marker}-2@simf.test";
        sheet.Cell(2, 1).Value = e1;
        sheet.Cell(2, 2).Value = "Imported Visitor One";
        // Type-smuggling attempt — must be ignored. The created user
        // must be a Visitor with no Administrator role.
        sheet.Cell(2, 4).Value = "Administrator";
        sheet.Cell(3, 1).Value = e2;
        sheet.Cell(3, 2).Value = "Imported Visitor Two";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var xlsx = ms.ToArray();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "visitors.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/visitors/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>())!;
        Assert.Equal(2, body.Data!.Created);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var created1 = await users.FindByEmailAsync(e1);
        Assert.NotNull(created1);
        Assert.Equal(UserType.Visitor, created1!.UserType);
        Assert.False(await users.IsInRoleAsync(created1, AdministratorRole),
            "A smuggled Administrator role in a visitor import must be silently dropped.");
    }

    // -- Authorisation --------------------------------------------------------

    [Fact]
    public async Task A_non_admin_caller_is_forbidden_from_visitor_bulk_delete()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { Guid.NewGuid() },
                Reason = "Non-admin caller must not reach this endpoint.",
            },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<(string Token, Guid AdminId)> CreateAdminAsync()
    {
        var email = $"vgrid-admin-{Guid.NewGuid():N}@simf.test";
        Guid id;
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
                DisplayName = "VGrid Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            id = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, id);
    }

    private async Task<Guid> CreateVisitorAsync(string adminToken, string? displayName = null)
    {
        var email = $"v-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = displayName ?? "Visitor Subject",
            },
            adminToken);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> CreateAdminVictimAsync(string adminToken)
    {
        var email = $"a-victim-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = email,
                DisplayName = "Admin Victim",
            },
            adminToken);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }
}
