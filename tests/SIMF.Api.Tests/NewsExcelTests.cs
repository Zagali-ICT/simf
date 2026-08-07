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
using SIMF.Contracts.PublicRelations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 generic grid Excel engine applied to News:
/// export round-trip, a positive import, a per-row duplicate-title error that
/// does not abort the batch, the upload-defence rejections (not-a-workbook,
/// wrong sheet) and the Export/Import permission gate.
/// </summary>
public sealed class NewsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public NewsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateNewsAsync(adminToken, $"Export A {Guid.NewGuid():N}");
        await CreateNewsAsync(adminToken, $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/news/export",
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
        var titleOne = $"Imported One {Guid.NewGuid():N}";
        var titleTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildNewsWorkbook("News",
            (titleOne, "عنوان ١", "Body one", "نص ١", "Press", "صحافة", 3),
            (titleTwo, "عنوان ٢", "Body two", "نص ٢", "Event", "فعالية", 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/news/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/news/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminNewsSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Title == titleOne);
        Assert.Contains(page.Items, item => item.Title == titleTwo);
    }

    [Fact]
    public async Task Export_includes_the_body_arabic_and_excerpt_arabic_columns()
    {
        // D-506 — the news Excel export must surface the Arabic body + excerpt,
        // not drop them at the IO boundary.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var title = $"Export Body {Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/news",
            new CreateNewsRequest
            {
                Title = title,
                TitleArabic = $"عنوان {Guid.NewGuid():N}",
                Excerpt = "English excerpt.",
                ExcerptArabic = "مقتطف عربي.",
                Body = "English body text.",
                BodyArabic = "نص الخبر بالعربية.",
                Category = "Press",
                CategoryArabic = "صحافة",
                PublishedAt = SimfClock.Now,
                DisplayOrder = 0,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/news/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("News");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("BodyArabic", headers);
        Assert.Contains("ExcerptArabic", headers);

        var titleCol = headers.IndexOf("Title") + 1;
        var bodyArabicCol = headers.IndexOf("BodyArabic") + 1;
        var excerptArabicCol = headers.IndexOf("ExcerptArabic") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(titleCol).GetString() == title);
        Assert.Equal("نص الخبر بالعربية.", dataRow.Cell(bodyArabicCol).GetString());
        Assert.Equal("مقتطف عربي.", dataRow.Cell(excerptArabicCol).GetString());
    }

    [Fact]
    public async Task Import_round_trips_the_body_arabic_and_excerpt_arabic()
    {
        // D-506 — an import workbook carrying BodyArabic/ExcerptArabic must persist
        // them (the summary the list returns now carries them too).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var title = $"Body XLSX {Guid.NewGuid():N}";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("News");
        sheet.Cell(1, 1).Value = "Title";
        sheet.Cell(1, 2).Value = "TitleArabic";
        sheet.Cell(1, 3).Value = "Body";
        sheet.Cell(1, 4).Value = "BodyArabic";
        sheet.Cell(1, 5).Value = "Category";
        sheet.Cell(1, 6).Value = "CategoryArabic";
        sheet.Cell(1, 7).Value = "ExcerptArabic";
        sheet.Cell(2, 1).Value = title;
        sheet.Cell(2, 2).Value = $"عنوان {Guid.NewGuid():N}";
        sheet.Cell(2, 3).Value = "English body text.";
        sheet.Cell(2, 4).Value = "نص الخبر بالعربية.";
        sheet.Cell(2, 5).Value = "Press";
        sheet.Cell(2, 6).Value = "صحافة";
        sheet.Cell(2, 7).Value = "مقتطف عربي.";
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/news/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/news/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminNewsSummary>>>())!.Data!;
        var created = page.Items.Single(item => item.Title == title);
        Assert.Equal("نص الخبر بالعربية.", created.BodyArabic);
        Assert.Equal("مقتطف عربي.", created.ExcerptArabic);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_duplicate_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existing = $"Dup {Guid.NewGuid():N}";
        await CreateNewsAsync(adminToken, existing);
        var fresh = $"Fresh {Guid.NewGuid():N}";

        // One duplicate row (must error) + one new row (must still be created).
        var workbook = BuildNewsWorkbook("News",
            (existing, "مكرر", "Body dup", "نص مكرر", "Press", "صحافة", 1),
            (fresh, "جديد", "Body fresh", "نص جديد", "Event", "فعالية", 2));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/news/import", workbook, adminToken);

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
            "/api/v1/admin/news/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildNewsWorkbook("NotNews",
            ($"X {Guid.NewGuid():N}", "س", "Body", "نص", "Press", "صحافة", 1));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/news/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/news/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static byte[] BuildNewsWorkbook(
        string sheetName,
        params (string Title, string TitleArabic, string Body, string BodyArabic,
            string Category, string CategoryArabic, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Title";
        sheet.Cell(1, 2).Value = "TitleArabic";
        sheet.Cell(1, 3).Value = "Body";
        sheet.Cell(1, 4).Value = "BodyArabic";
        sheet.Cell(1, 5).Value = "Category";
        sheet.Cell(1, 6).Value = "CategoryArabic";
        sheet.Cell(1, 7).Value = "PublishedAt";
        sheet.Cell(1, 8).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Title;
            sheet.Cell(i + 2, 2).Value = rows[i].TitleArabic;
            sheet.Cell(i + 2, 3).Value = rows[i].Body;
            sheet.Cell(i + 2, 4).Value = rows[i].BodyArabic;
            sheet.Cell(i + 2, 5).Value = rows[i].Category;
            sheet.Cell(i + 2, 6).Value = rows[i].CategoryArabic;
            sheet.Cell(i + 2, 7).Value = SimfClock.Now.ToString("O");
            sheet.Cell(i + 2, 8).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateNewsAsync(string token, string title)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/news",
            new CreateNewsRequest
            {
                Title = title,
                TitleArabic = "عنوان",
                Body = "Body text.",
                BodyArabic = "نص الخبر.",
                Category = "Press",
                CategoryArabic = "صحافة",
                PublishedAt = SimfClock.Now,
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"news-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test News Excel Admin",
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
