using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the SimfDataGrid v2 admin endpoints (decision D-044 b)
/// — bulk delete, duplicate, export (XLSX), import (XLSX).
/// </summary>
public sealed class AdminGridV2Tests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminGridV2Tests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- Bulk delete -----------------------------------------------------------

    [Fact]
    public async Task Bulk_delete_disables_the_targets_and_skips_admins_and_self()
    {
        var (adminToken, adminId) = await CreateAdminAsync();

        var target1 = await CreateRegularUserAsync(adminToken);
        var target2 = await CreateRegularUserAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                // Includes the admin themselves and one regular target — the
                // self-target is skipped, the regular targets succeed.
                Ids = new List<Guid> { target1, target2, adminId },
                Reason = "Bulk-delete test — multiple targets including self.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(2, body.Data!.Deleted);
        Assert.Equal(1, body.Data.Skipped);

        // The two regular users are now in the Disabled state.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var t1 = await db.Users.SingleAsync(u => u.Id == target1);
        var t2 = await db.Users.SingleAsync(u => u.Id == target2);
        Assert.Equal(AccountState.Disabled, t1.AccountState);
        Assert.Equal(AccountState.Disabled, t2.AccountState);
    }

    [Fact]
    public async Task Bulk_delete_skips_administrator_targets_and_writes_one_audit_row_per_subject()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var regular = await CreateRegularUserAsync(adminToken);

        // Promote a second user to Administrator so we can hit the
        // admin-vs-admin skip branch.
        var (_, peerAdminId) = await CreateAdminAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { regular, peerAdminId, Guid.NewGuid() },
                Reason = "Hardening test — one regular, one admin peer, one unknown id.",
            },
            adminToken);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(1, body.Data!.Deleted);
        Assert.Equal(2, body.Data.Skipped);

        // Audit: one Admin.UserDeleted (success) row + two
        // Admin.UserDeleteFailed (failure) rows must be written for this
        // batch — the SOC visibility contract from D-044(b) / D-045 H1.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var rows = await db.OperationLog
            .Where(row => row.SubjectUserId == regular
                || row.SubjectUserId == peerAdminId)
            .ToListAsync();
        Assert.Contains(rows, row => row.SubjectUserId == regular
            && row.EventType == "Admin.UserDeleted");
        Assert.Contains(rows, row => row.SubjectUserId == peerAdminId
            && row.EventType == "Admin.UserDeleteFailed");
    }

    [Fact]
    public async Task Bulk_delete_rejects_a_batch_larger_than_500_ids()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var hugeBatch = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = hugeBatch,
                Reason = "A batch over the 500-id cap must be rejected.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_requires_a_reason_of_at_least_ten_characters()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var target = await CreateRegularUserAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { target },
                Reason = "short",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Duplicate -------------------------------------------------------------

    [Fact]
    public async Task Duplicate_creates_a_new_user_with_the_same_display_name()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var sourceId = await CreateRegularUserAsync(adminToken, displayName: "Source Name");
        var newEmail = $"dup-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = newEmail },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        Assert.Equal(newEmail, body.Data!.Email);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var copy = await users.FindByEmailAsync(newEmail);
        Assert.Equal("Source Name", copy!.DisplayName);
    }

    [Fact]
    public async Task Duplicate_with_a_colliding_email_returns_409()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var sourceId = await CreateRegularUserAsync(adminToken, displayName: "Source");
        // Create a second user we'll then try to duplicate over.
        var collidingEmail = $"already-{Guid.NewGuid():N}@simf.test";
        await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest { Email = collidingEmail, DisplayName = "Already" },
            adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = collidingEmail },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_with_an_invalid_email_returns_400()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var sourceId = await CreateRegularUserAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest { SourceId = sourceId, NewEmail = "not-an-email" },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_with_an_unknown_source_returns_404()
    {
        var (adminToken, _) = await CreateAdminAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/duplicate",
            new AdminDuplicateUserRequest
            {
                SourceId = Guid.NewGuid(),
                NewEmail = $"missing-{Guid.NewGuid():N}@simf.test",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- Export (XLSX) ---------------------------------------------------------

    [Fact]
    public async Task Export_returns_an_xlsx_workbook_with_the_selected_users()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var u1 = await CreateRegularUserAsync(adminToken, displayName: "Export A");
        var u2 = await CreateRegularUserAsync(adminToken, displayName: "Export B");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins/export")
        {
            Content = JsonContent.Create(
                new AdminExportUsersRequest { Ids = new List<Guid> { u1, u2 } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Users");
        Assert.Equal("Email", sheet.Cell(1, 1).GetString());

        var displayNames = new[]
        {
            sheet.Cell(2, 2).GetString(),
            sheet.Cell(3, 2).GetString(),
        };
        Assert.Contains("Export A", displayNames);
        Assert.Contains("Export B", displayNames);
    }

    // -- Import (XLSX) ---------------------------------------------------------

    [Fact]
    public async Task Import_creates_users_from_a_round_tripped_workbook()
    {
        var (adminToken, _) = await CreateAdminAsync();

        // Build a small workbook in-memory.
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 4).Value = "Role";
        var marker = $"imp{Guid.NewGuid():N}".Substring(0, 8);
        var e1 = $"{marker}-1@simf.test";
        var e2 = $"{marker}-2@simf.test";
        sheet.Cell(2, 1).Value = e1; sheet.Cell(2, 2).Value = "Imported One";
        sheet.Cell(3, 1).Value = e2; sheet.Cell(3, 2).Value = "Imported Two";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var xlsx = ms.ToArray();

        // Upload as multipart form.
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "import.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins/import")
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
        Assert.NotNull(await users.FindByEmailAsync(e1));
        Assert.NotNull(await users.FindByEmailAsync(e2));
    }

    [Fact]
    public async Task Import_rejects_a_workbook_whose_sheet_is_not_named_Users()
    {
        var (adminToken, _) = await CreateAdminAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");      // wrong name on purpose
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(2, 1).Value = $"sneak-{Guid.NewGuid():N}@simf.test";
        sheet.Cell(2, 2).Value = "Sneaky Sheet";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(ms.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "wrong-sheet.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_escapes_formula_injection_attempts_in_string_cells()
    {
        var (adminToken, _) = await CreateAdminAsync();
        // Display name carrying a formula that Excel would auto-execute.
        var displayName = "=HYPERLINK(\"https://attacker.example/x\",\"Click\")";
        var userId = await CreateRegularUserAsync(adminToken, displayName: displayName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins/export")
        {
            Content = JsonContent.Create(
                new AdminExportUsersRequest { Ids = new List<Guid> { userId } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Users");
        var cell = sheet.Cell(2, 2);
        // D-045 H1 — the cell must be stored as text, NEVER as a formula.
        // GetString preserves the visible value; the apostrophe prefix
        // ClosedXmlUserExcelService.SanitiseForExcel writes is the
        // text-marker that disarms Excel's formula evaluator.
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.True(string.IsNullOrEmpty(cell.FormulaA1),
            "The cell must not carry a live formula — formula injection guard failed.");
        Assert.Contains("HYPERLINK", cell.GetString());
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_an_xlsx()
    {
        var (adminToken, _) = await CreateAdminAsync();

        var garbage = System.Text.Encoding.UTF8.GetBytes("not a workbook");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(garbage), "file", "fake.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<(string Token, Guid AdminId)> CreateAdminAsync()
    {
        var email = $"gridv2-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "GridV2 Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,   // P7b — audience gate keys off UserType
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            id = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,   // P2 — admin helper
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, id);
    }

    private async Task<Guid> CreateRegularUserAsync(string adminToken, string? displayName = null)
    {
        var email = $"u-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/admins",
            new AdminCreateAdminRequest
            {
                Email = email,
                DisplayName = displayName ?? "Regular User",
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
