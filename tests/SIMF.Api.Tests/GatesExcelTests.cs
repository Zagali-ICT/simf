using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 generic grid Excel engine, exercised through
/// the Gates resource: export round-trip, a positive import, the upload-defence
/// rejections (not-a-workbook, wrong sheet) and the Export permission gate.
/// </summary>
public sealed class GatesExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GatesExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateGateAsync(adminToken, NewGateCode());
        await CreateGateAsync(adminToken, NewGateCode());

        var response = await PostAuthAsync(
            "/api/v1/admin/gates/export",
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
        var codeOne = NewGateCode();
        var codeTwo = NewGateCode();
        var workbook = BuildGatesWorkbook("Gates",
            (codeOne, "Imported Gate One", "بوابة مستوردة ١", "Both"),
            (codeTwo, "Imported Gate Two", "بوابة مستوردة ٢", "In"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/gates/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed (gate codes are upper-cased on save).
        var list = await PostAuthAsync(
            "/api/v1/admin/gates/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminGateSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Code == codeOne.ToUpperInvariant());
        Assert.Contains(page.Items, item => item.Code == codeTwo.ToUpperInvariant());
    }

    [Fact]
    public async Task Export_includes_the_description_columns()
    {
        // D-506 — the gate Excel export must surface the bilingual description,
        // not drop it at the IO boundary.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = NewGateCode();
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Gate With Description",
                NameArabic = "بوابة بوصف",
                DirectionMode = DirectionMode.Both,
                Description = "Main north entrance.",
                DescriptionArabic = "المدخل الشمالي الرئيسي.",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/gates/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Gates");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Description", headers);
        Assert.Contains("DescriptionArabic", headers);

        var codeCol = headers.IndexOf("Code") + 1;
        var descCol = headers.IndexOf("Description") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(codeCol).GetString() == code.ToUpperInvariant());
        Assert.Equal("Main north entrance.", dataRow.Cell(descCol).GetString());
    }

    [Fact]
    public async Task Import_round_trips_the_description()
    {
        // D-506 — an import workbook carrying Description/DescriptionArabic must
        // persist them (the summary the list returns now carries them too).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = NewGateCode();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Gates");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "Description";
        sheet.Cell(1, 5).Value = "DescriptionArabic";
        sheet.Cell(2, 1).Value = code;
        sheet.Cell(2, 2).Value = "Gate XLSX Description";
        sheet.Cell(2, 3).Value = "بوابة وصف";
        sheet.Cell(2, 4).Value = "Main north entrance.";
        sheet.Cell(2, 5).Value = "المدخل الشمالي الرئيسي.";
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/gates/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/gates/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminGateSummary>>>())!.Data!;
        var created = page.Items.Single(item => item.Code == code.ToUpperInvariant());
        Assert.Equal("Main north entrance.", created.Description);
        Assert.Equal("المدخل الشمالي الرئيسي.", created.DescriptionArabic);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_duplicate_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existing = NewGateCode();
        await CreateGateAsync(adminToken, existing);
        var fresh = NewGateCode();

        // One duplicate row (must error) + one new row (must still be created).
        var workbook = BuildGatesWorkbook("Gates",
            (existing, "Duplicate Gate", "بوابة مكررة", "Both"),
            (fresh, "Fresh Gate", "بوابة جديدة", "Out"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/gates/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(existing, result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/gates/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildGatesWorkbook("NotGates",
            (NewGateCode(), "Wrong Sheet", "ورقة خاطئة", "Both"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/gates/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/gates/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static string NewGateCode() =>
        $"GX{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";

    private static byte[] BuildGatesWorkbook(
        string sheetName,
        params (string Code, string Name, string NameArabic, string DirectionMode)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "DirectionMode";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Name;
            sheet.Cell(i + 2, 3).Value = rows[i].NameArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].DirectionMode;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateGateAsync(string token, string code)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Gate",
                NameArabic = "بوابة",
                DirectionMode = DirectionMode.Both,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"gate-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Gate Excel Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
