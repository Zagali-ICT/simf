using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 generic grid Excel engine applied to
/// Themes: export round-trip, a positive import, a per-row duplicate-code
/// error that does not abort the batch, the upload-defence rejections
/// (not-a-workbook, wrong sheet) and the Export/Import permission gate.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ThemesExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ThemesExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateThemeAsync(adminToken, NewCode(), $"Export A {Guid.NewGuid():N}");
        await CreateThemeAsync(adminToken, NewCode(), $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/themes/export",
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
        var codeOne = NewCode();
        var codeTwo = NewCode();
        var nameOne = $"Imported One {Guid.NewGuid():N}";
        var nameTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildThemesWorkbook("Themes",
            (codeOne, nameOne, "مستورد ١", "#102A43", 3),
            (codeTwo, nameTwo, "مستورد ٢", "#0B7285", 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/themes/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/themes/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminThemeSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Name == nameOne);
        Assert.Contains(page.Items, item => item.Name == nameTwo);
    }

    [Fact]
    public async Task Export_includes_the_description_columns()
    {
        // D-506 — the theme Excel export must surface the bilingual descriptions,
        // not drop them at the IO boundary.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var name = $"Export Desc {Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/themes",
            new AdminCreateThemeRequest
            {
                Code = code,
                Name = name,
                NameArabic = "محور الوصف",
                PageColor = "#102A43",
                DisplayOrder = 0,
                Description = "Maritime security track.",
                DescriptionArabic = "مسار الأمن البحري.",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/themes/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Themes");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Description", headers);
        Assert.Contains("DescriptionArabic", headers);

        var codeCol = headers.IndexOf("Code") + 1;
        var descCol = headers.IndexOf("Description") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(codeCol).GetString() == code);
        Assert.Equal("Maritime security track.", dataRow.Cell(descCol).GetString());
    }

    [Fact]
    public async Task Import_round_trips_the_description()
    {
        // D-506 — an import workbook carrying Description/DescriptionArabic must
        // persist them; the GET detail then surfaces them back.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = NewCode();
        var name = $"Desc XLSX {Guid.NewGuid():N}";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Themes");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "PageColor";
        sheet.Cell(1, 5).Value = "Description";
        sheet.Cell(1, 6).Value = "DescriptionArabic";
        sheet.Cell(2, 1).Value = code;
        sheet.Cell(2, 2).Value = name;
        sheet.Cell(2, 3).Value = "محور مستورد";
        sheet.Cell(2, 4).Value = "#0B7285";
        sheet.Cell(2, 5).Value = "Maritime security track.";
        sheet.Cell(2, 6).Value = "مسار الأمن البحري.";
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/themes/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        // Find the created row in the grid (the summary now carries the
        // descriptions too) then read the GET detail to assert both.
        var list = await PostAuthAsync(
            "/api/v1/admin/themes/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminThemeSummary>>>())!.Data!;
        var created = page.Items.Single(item => item.Code == code);
        Assert.Equal("Maritime security track.", created.Description);
        Assert.Equal("مسار الأمن البحري.", created.DescriptionArabic);

        var detail = await GetAuthAsync(
            $"/api/v1/admin/themes/{created.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var theme = (await detail.Content
            .ReadFromJsonAsync<ApiResult<AdminThemeDetail>>())!.Data!;
        Assert.Equal("Maritime security track.", theme.Description);
        Assert.Equal("مسار الأمن البحري.", theme.DescriptionArabic);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_duplicate_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existing = NewCode();
        await CreateThemeAsync(adminToken, existing, $"Dup {Guid.NewGuid():N}");
        var fresh = NewCode();

        // One duplicate row (must error) + one new row (must still be created).
        var workbook = BuildThemesWorkbook("Themes",
            (existing, $"Dup name {Guid.NewGuid():N}", "مكرر", "#102A43", 1),
            (fresh, $"Fresh {Guid.NewGuid():N}", "جديد", "#0B7285", 2));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/themes/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        // The service upper-cases the code, so the echoed key matches the input.
        Assert.Equal(existing, result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/themes/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildThemesWorkbook("NotThemes",
            (NewCode(), $"X {Guid.NewGuid():N}", "س", "#102A43", 1));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/themes/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/themes/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    /// <summary>A unique 2–16 char upper-case theme code (the service upper-cases
    /// and bounds it; we generate within those bounds so the row is valid).</summary>
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();

    private static byte[] BuildThemesWorkbook(
        string sheetName,
        params (string Code, string Name, string NameArabic, string PageColor, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "PageColor";
        sheet.Cell(1, 5).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Name;
            sheet.Cell(i + 2, 3).Value = rows[i].NameArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].PageColor;
            sheet.Cell(i + 2, 5).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateThemeAsync(string token, string code, string name)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/themes",
            new AdminCreateThemeRequest
            {
                Code = code,
                Name = name,
                NameArabic = "محور",
                PageColor = "#102A43",
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"theme-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Theme Excel Admin",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
