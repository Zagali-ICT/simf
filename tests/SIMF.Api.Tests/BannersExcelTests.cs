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
/// Banners: export round-trip, a positive import, a per-row invalid-window
/// error that does not abort the batch, the upload-defence rejections
/// (not-a-workbook, wrong sheet) and the Export/Import permission gate.
/// </summary>
public sealed class BannersExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BannersExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateBannerAsync(adminToken, $"Export A {Guid.NewGuid():N}");
        await CreateBannerAsync(adminToken, $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/banners/export",
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
        var start = SimfClock.Now;
        var end = start.AddDays(7);
        var titleOne = $"Imported One {Guid.NewGuid():N}";
        var titleTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildBannersWorkbook("Banners",
            (titleOne, "مستورد ١", "Body one", "نص ١", start, end, 3),
            (titleTwo, "مستورد ٢", "Body two", "نص ٢", start, end, 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/banners/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/banners/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBannerSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Title == titleOne);
        Assert.Contains(page.Items, item => item.Title == titleTwo);
    }

    [Fact]
    public async Task Export_includes_the_body_and_link_columns_but_not_the_image()
    {
        // D-506 — the export must now carry Body/BodyArabic/ImageUrl/LinkUrl so a
        // round-trip through import does not drop them.
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateBannerAsync(adminToken, $"Export cols {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/banners/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 100 } },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Banners");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Body", headers);
        Assert.Contains("BodyArabic", headers);
        Assert.Contains("LinkUrl", headers);
        // The image is a StoredFile, uploaded rather than typed, so there is no
        // column for a spreadsheet to carry and none is exported.
        Assert.DoesNotContain("ImageUrl", headers);
    }

    [Fact]
    public async Task Import_round_trips_the_body_and_link_and_ignores_a_stale_image_column()
    {
        // D-506 — a workbook carrying Body/BodyArabic/LinkUrl must persist all
        // three onto the created banner (the GET detail proves they are not
        // dropped on import).
        //
        // The workbook still carries an ImageUrl column, deliberately: the image
        // is a StoredFile now and the importer ignores that column, so this also
        // proves a workbook exported before the change still imports cleanly
        // instead of failing on an unknown header.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var start = SimfClock.Now;
        var end = start.AddDays(7);
        var title = $"Round trip {Guid.NewGuid():N}";
        const string body = "Round-trip body";
        const string bodyArabic = "نص الجولة";
        const string imageUrl = "https://example.test/banner.png";
        const string linkUrl = "https://example.test/landing";
        var workbook = BuildBannersWorkbook("Banners",
            (title, "مستورد", body, bodyArabic, imageUrl, linkUrl, start, end, 5));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/banners/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);

        // Find the created banner via the list, then load its detail.
        var list = await PostAuthAsync(
            "/api/v1/admin/banners/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBannerSummary>>>())!.Data!;
        var summary = Assert.Single(page.Items, item => item.Title == title);

        var detailResponse = await GetAuthAsync(
            $"/api/v1/admin/banners/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminBannerDetail>>())!.Data!;
        Assert.Equal(body, detail.Body);
        Assert.Equal(bodyArabic, detail.BodyArabic);
        Assert.Equal(linkUrl, detail.LinkUrl);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_an_invalid_window_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var start = SimfClock.Now;
        var end = start.AddDays(7);
        var badTitle = $"Bad window {Guid.NewGuid():N}";
        var goodTitle = $"Good {Guid.NewGuid():N}";

        // One bad row (end before start → service rejects) + one valid row
        // (must still be created).
        var workbook = BuildBannersWorkbook("Banners",
            (badTitle, "سيئ", "Body", "نص", end, start, 1),
            (goodTitle, "جيد", "Body", "نص", start, end, 2));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/banners/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(badTitle, result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/banners/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var start = SimfClock.Now;
        var wrongSheet = BuildBannersWorkbook("NotBanners",
            ($"X {Guid.NewGuid():N}", "س", "Body", "نص", start, start.AddDays(1), 1));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/banners/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/banners/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static byte[] BuildBannersWorkbook(
        string sheetName,
        params (string Title, string TitleArabic, string Body, string BodyArabic,
            DateTime Start, DateTime End, int DisplayOrder)[] rows) =>
        BuildBannersWorkbook(sheetName,
            rows.Select(r => (r.Title, r.TitleArabic, r.Body, r.BodyArabic,
                (string?)null, (string?)null, r.Start, r.End, r.DisplayOrder)).ToArray());

    // D-506 — overload that also writes the optional ImageUrl + LinkUrl columns
    // so the import round-trip test can carry them.
    private static byte[] BuildBannersWorkbook(
        string sheetName,
        params (string Title, string TitleArabic, string Body, string BodyArabic,
            string? ImageUrl, string? LinkUrl,
            DateTime Start, DateTime End, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Title";
        sheet.Cell(1, 2).Value = "TitleArabic";
        sheet.Cell(1, 3).Value = "Body";
        sheet.Cell(1, 4).Value = "BodyArabic";
        sheet.Cell(1, 5).Value = "ImageUrl";
        sheet.Cell(1, 6).Value = "LinkUrl";
        sheet.Cell(1, 7).Value = "Start";
        sheet.Cell(1, 8).Value = "End";
        sheet.Cell(1, 9).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Title;
            sheet.Cell(i + 2, 2).Value = rows[i].TitleArabic;
            sheet.Cell(i + 2, 3).Value = rows[i].Body;
            sheet.Cell(i + 2, 4).Value = rows[i].BodyArabic;
            sheet.Cell(i + 2, 5).Value = rows[i].ImageUrl ?? string.Empty;
            sheet.Cell(i + 2, 6).Value = rows[i].LinkUrl ?? string.Empty;
            sheet.Cell(i + 2, 7).Value = rows[i].Start.ToString("O");
            sheet.Cell(i + 2, 8).Value = rows[i].End.ToString("O");
            sheet.Cell(i + 2, 9).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateBannerAsync(string token, string title)
    {
        var start = SimfClock.Now;
        var response = await PostAuthAsync(
            "/api/v1/admin/banners",
            new CreateBannerRequest
            {
                Title = title,
                TitleArabic = "بانر",
                Body = "Body",
                BodyArabic = "نص",
                Start = start,
                End = start.AddDays(7),
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"banner-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Banner Excel Admin",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
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