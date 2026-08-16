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
/// Integration tests for the D-356 Sponsors grid Excel engine: the export
/// round-trip (ZIP magic), the Export permission gate, and an insert-only import
/// round-trip that maps the Tier display name back to its int value.
/// </summary>
public sealed class SponsorsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SponsorsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateSponsorAsync(adminToken, $"Export A {Guid.NewGuid():N}");
        await CreateSponsorAsync(adminToken, $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors/export",
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
        var nameOne = $"Imported One {Guid.NewGuid():N}";
        var nameTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildSponsorsWorkbook("Sponsors",
            (nameOne, "راعٍ ١", "Gold", 3),
            (nameTwo, "راعٍ ٢", "Bronze", 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sponsors/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/sponsors/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSponsorSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.NameEn == nameOne);
        Assert.Contains(page.Items, item => item.NameEn == nameTwo);
    }

    [Fact]
    public async Task Export_includes_the_tagline_and_about_columns()
    {
        // D-502 — the sponsor Excel export must surface the bilingual tagline +
        // about, not drop them at the IO boundary.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var nameEn = $"Export Tagline {Guid.NewGuid():N}";
        var create = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = nameEn,
                NameAr = $"شعار {Guid.NewGuid():N}",
                Tier = 20,
                DisplayOrder = 0,
                Tagline = "Strategic Partner",
                About = "A global energy leader.",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Sponsors");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Tagline", headers);
        Assert.Contains("TaglineArabic", headers);
        Assert.Contains("About", headers);
        Assert.Contains("AboutArabic", headers);
        // A logo is a file in the store, not a cell — the workbook must not
        // carry a logo column that could only ever export blank.
        Assert.DoesNotContain("LogoRelativePath", headers);

        var nameCol = headers.IndexOf("NameEn") + 1;
        var tagCol = headers.IndexOf("Tagline") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(nameCol).GetString() == nameEn);
        Assert.Equal("Strategic Partner", dataRow.Cell(tagCol).GetString());
    }

    [Fact]
    public async Task Import_round_trips_the_tagline_and_about()
    {
        // D-502 — an import workbook carrying Tagline/TaglineArabic/About/AboutArabic
        // must persist them (the summary the list returns now carries them too).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var nameEn = $"Tagline XLSX {Guid.NewGuid():N}";

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sponsors");
        sheet.Cell(1, 1).Value = "NameEn";
        sheet.Cell(1, 2).Value = "NameAr";
        sheet.Cell(1, 3).Value = "Tier";
        sheet.Cell(1, 4).Value = "Tagline";
        sheet.Cell(1, 5).Value = "TaglineArabic";
        sheet.Cell(1, 6).Value = "About";
        sheet.Cell(1, 7).Value = "AboutArabic";
        sheet.Cell(2, 1).Value = nameEn;
        sheet.Cell(2, 2).Value = $"شعار {Guid.NewGuid():N}";
        sheet.Cell(2, 3).Value = "Gold";
        sheet.Cell(2, 4).Value = "Strategic Partner";
        sheet.Cell(2, 5).Value = "الشريك الاستراتيجي";
        sheet.Cell(2, 6).Value = "A global energy leader.";
        sheet.Cell(2, 7).Value = "شركة طاقة عالمية.";
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sponsors/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/sponsors/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSponsorSummary>>>())!.Data!;
        var created = page.Items.Single(item => item.NameEn == nameEn);
        Assert.Equal("Strategic Partner", created.Tagline);
        Assert.Equal("الشريك الاستراتيجي", created.TaglineArabic);
        Assert.Equal("A global energy leader.", created.About);
        Assert.Equal("شركة طاقة عالمية.", created.AboutArabic);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static byte[] BuildSponsorsWorkbook(
        string sheetName, params (string NameEn, string NameAr, string Tier, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "NameEn";
        sheet.Cell(1, 2).Value = "NameAr";
        sheet.Cell(1, 3).Value = "Tier";
        sheet.Cell(1, 4).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].NameEn;
            // Sponsors dedup on (Tier, NameAr); keep each imported Arabic name
            // unique so the test is robust on the non-reset integration DB.
            sheet.Cell(i + 2, 2).Value = $"{rows[i].NameAr} {Guid.NewGuid():N}";
            sheet.Cell(i + 2, 3).Value = rows[i].Tier;
            sheet.Cell(i + 2, 4).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateSponsorAsync(string token, string nameEn)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = nameEn,
                NameAr = $"راعٍ {Guid.NewGuid():N}",
                Tier = 10,
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sponsor-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Sponsor Excel Admin",
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
