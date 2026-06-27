using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 Exhibitors grid Excel engine: the export
/// round-trip (ZIP magic), an insert-only import round-trip (the created rows
/// appear in the list) and the Export permission gate.
/// </summary>
public sealed class ExhibitorsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/exhibitors/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 100 } },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // Every .xlsx is a ZIP — the first four bytes are the local-file header.
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public async Task Import_creates_each_row_and_reports_the_outcome()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var nameOne = $"Imported Exhibitor One {Guid.NewGuid():N}";
        var nameTwo = $"Imported Exhibitor Two {Guid.NewGuid():N}";
        var workbook = BuildExhibitorsWorkbook("Exhibitors",
            (nameOne, "عارض مستورد ١"),
            (nameTwo, "عارض مستورد ٢"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/exhibitors/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/exhibitors/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminExhibitorSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.NameEn == nameOne);
        Assert.Contains(page.Items, item => item.NameEn == nameTwo);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_blank_name_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var fresh = $"Fresh Exhibitor {Guid.NewGuid():N}";

        // One invalid row (blank English name → error) + one valid row (created).
        var workbook = BuildExhibitorsWorkbook("Exhibitors",
            (string.Empty, "بدون اسم إنجليزي"),
            (fresh, "عارض جديد"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/exhibitors/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task Excel_round_trips_the_tier()
    {
        // D-503 — import a workbook carrying a Tier column, confirm the list
        // summary carries it, then export and confirm the Tier column round-trips
        // (it was previously dropped on both the import and export sides).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var nameEn = $"Tier Exhibitor {Guid.NewGuid():N}";

        using var inWorkbook = new XLWorkbook();
        var inSheet = inWorkbook.Worksheets.Add("Exhibitors");
        inSheet.Cell(1, 1).Value = "NameEn";
        inSheet.Cell(1, 2).Value = "NameAr";
        inSheet.Cell(1, 3).Value = "Tier";
        inSheet.Cell(2, 1).Value = nameEn;
        inSheet.Cell(2, 2).Value = $"عارض {Guid.NewGuid():N}";
        inSheet.Cell(2, 3).Value = "Premium";
        byte[] importBytes;
        using (var stream = new MemoryStream())
        {
            inWorkbook.SaveAs(stream);
            importBytes = stream.ToArray();
        }

        var import = await PostFileAuthAsync(
            "/api/v1/admin/exhibitors/import", importBytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var result = (await import.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/exhibitors/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminExhibitorSummary>>>())!.Data!;
        var created = page.Items.Single(item => item.NameEn == nameEn);
        Assert.Equal(ExhibitorTier.Premium, created.Tier);

        var export = await PostAuthAsync(
            "/api/v1/admin/exhibitors/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var bytes = await export.Content.ReadAsByteArrayAsync();
        using var outStream = new MemoryStream(bytes);
        using var outWorkbook = new XLWorkbook(outStream);
        var sheet = outWorkbook.Worksheet("Exhibitors");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Tier", headers);
        var nameCol = headers.IndexOf("NameEn") + 1;
        var tierCol = headers.IndexOf("Tier") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(nameCol).GetString() == nameEn);
        Assert.Equal("Premium", dataRow.Cell(tierCol).GetString());
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_an_unknown_tier()
    {
        // D-503 — an unknown non-blank tier is a per-row error (not a batch abort);
        // a blank tier is valid (null).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var validRow = $"Blank Tier Exhibitor {Guid.NewGuid():N}";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Exhibitors");
        sheet.Cell(1, 1).Value = "NameEn";
        sheet.Cell(1, 2).Value = "NameAr";
        sheet.Cell(1, 3).Value = "Tier";
        sheet.Cell(2, 1).Value = $"Bad Tier Exhibitor {Guid.NewGuid():N}";
        sheet.Cell(2, 2).Value = $"عارض {Guid.NewGuid():N}";
        sheet.Cell(2, 3).Value = "Diamond";
        sheet.Cell(3, 1).Value = validRow;
        sheet.Cell(3, 2).Value = $"عارض {Guid.NewGuid():N}";
        sheet.Cell(3, 3).Value = string.Empty;
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/exhibitors/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/exhibitors/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static byte[] BuildExhibitorsWorkbook(
        string sheetName, params (string NameEn, string NameAr)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "NameEn";
        sheet.Cell(1, 2).Value = "NameAr";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].NameEn;
            sheet.Cell(i + 2, 2).Value = rows[i].NameAr;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"exhibitor-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Exhibitor Excel Admin",
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

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostFileAuthAsync(string url, byte[] xlsx, string token)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);
        content.Add(file, "file", "import.xlsx");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
