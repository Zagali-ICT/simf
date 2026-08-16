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
/// Integration tests for the D-356 Speakers grid Excel engine: export round-trip
/// (ZIP-magic assertion), the Export permission gate, and an import round-trip
/// (Code/Name/NameArabic) asserting the rows are created and then listed.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SpeakersExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SpeakersExcelTests(SimfApiFactory factory)
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
            "/api/v1/admin/speakers/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 100 } },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // .xlsx is a ZIP — first four bytes are the local-file header.
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public async Task Import_creates_each_row_and_reports_the_outcome()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var codeOne = RandomCode();
        var codeTwo = RandomCode();
        var nameOne = $"Imported One {Guid.NewGuid():N}";
        var nameTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildSpeakersWorkbook("Speakers",
            (codeOne, nameOne, "متحدّث ١", "Captain", 3),
            (codeTwo, nameTwo, "متحدّث ٢", "Commander", 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/speakers/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 2);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/speakers/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Name == nameOne);
        Assert.Contains(page.Items, item => item.Name == nameTwo);
    }

    [Fact]
    public async Task Import_round_trips_the_arabic_rank()
    {
        // Regression: the importer used to bind only the English "Rank" column and
        // silently dropped the Arabic rank, so Excel-created speakers landed with
        // RankArabic = null and the Arabic app fell back to the English rank. This
        // asserts a populated "RankArabic" column persists, and a blank one persists
        // null (the intended fallback), through the same list projection the grid reads.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var withArabicCode = RandomCode();
        var blankArabicCode = RandomCode();
        var withArabicName = $"With Arabic Rank {Guid.NewGuid():N}";
        var blankArabicName = $"Blank Arabic Rank {Guid.NewGuid():N}";
        const string arabicRank = "القبطان البحري";
        var workbook = BuildSpeakersWorkbookWithArabicRank("Speakers",
            (withArabicCode, withArabicName, "متحدّث ١", "Captain", arabicRank, 5),
            (blankArabicCode, blankArabicName, "متحدّث ٢", "Commander", string.Empty, 6));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/speakers/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 2);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/speakers/list", new GridQuery { Top = 500 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerSummary>>>())!.Data!;

        // A populated Arabic-rank cell round-trips into RankArabic.
        var withArabic = page.Items.Single(item => item.Name == withArabicName);
        Assert.Equal(arabicRank, withArabic.RankArabic);
        Assert.Equal("Captain", withArabic.Rank);

        // A blank Arabic-rank cell persists as null (the intended English fallback).
        var blankArabic = page.Items.Single(item => item.Name == blankArabicName);
        Assert.Null(blankArabic.RankArabic);
        Assert.Equal("Commander", blankArabic.Rank);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/speakers/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static string RandomCode() => $"SPK{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static byte[] BuildSpeakersWorkbook(
        string sheetName,
        params (string Code, string Name, string NameArabic, string Rank, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "Rank";
        sheet.Cell(1, 5).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Name;
            sheet.Cell(i + 2, 3).Value = rows[i].NameArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].Rank;
            sheet.Cell(i + 2, 5).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSpeakersWorkbookWithArabicRank(
        string sheetName,
        params (string Code, string Name, string NameArabic, string Rank,
            string RankArabic, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "NameArabic";
        sheet.Cell(1, 4).Value = "Rank";
        sheet.Cell(1, 5).Value = "RankArabic";
        sheet.Cell(1, 6).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Name;
            sheet.Cell(i + 2, 3).Value = rows[i].NameArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].Rank;
            sheet.Cell(i + 2, 5).Value = rows[i].RankArabic;
            sheet.Cell(i + 2, 6).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"speaker-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Speaker Excel Admin",
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
