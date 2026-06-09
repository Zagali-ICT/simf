using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 generic grid Excel engine applied to the
/// Archive editions resource: export round-trip, a positive import, a per-row
/// duplicate-year error that does not abort the batch, the upload-defence
/// rejections (not-a-workbook, wrong sheet) and the Export permission gate.
/// </summary>
public sealed class ArchiveExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ArchiveExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateEditionAsync(adminToken, NewYear(), $"Export A {Guid.NewGuid():N}");
        await CreateEditionAsync(adminToken, NewYear(), $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/archive/export",
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
        var yearOne = NewYear();
        var yearTwo = NewYear();
        var titleOne = $"Imported One {Guid.NewGuid():N}";
        var titleTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildArchiveWorkbook("Archive",
            (yearOne, titleOne, "مستورد ١", 100, 10, 5),
            (yearTwo, titleTwo, "مستورد ٢", 200, 20, 8));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/archive/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created editions are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/archive/list", new GridQuery { Top = 500 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminArchiveEditionSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.TitleEn == titleOne);
        Assert.Contains(page.Items, item => item.TitleEn == titleTwo);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_duplicate_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existing = NewYear();
        await CreateEditionAsync(adminToken, existing, $"Dup {Guid.NewGuid():N}");
        var fresh = NewYear();

        // One duplicate-year row (must error) + one new row (must still be created).
        var workbook = BuildArchiveWorkbook("Archive",
            (existing, $"Dup title {Guid.NewGuid():N}", "مكرر", 1, 1, 1),
            (fresh, $"Fresh {Guid.NewGuid():N}", "جديد", 2, 2, 2));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/archive/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        // The per-row key echoes the offending row's year.
        Assert.Equal(existing.ToString(), result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/archive/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildArchiveWorkbook("NotArchive",
            (NewYear(), $"X {Guid.NewGuid():N}", "س", 1, 1, 1));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/archive/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/archive/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    // One archive edition exists per year and the service bounds the year to
    // 2000–2100; a static cursor hands each row a unique year within range so
    // concurrent test methods never clash on the same year.
    private static int _yearCursor = 2000;
    private static int NewYear() => Interlocked.Increment(ref _yearCursor);

    private static byte[] BuildArchiveWorkbook(
        string sheetName,
        params (int Year, string TitleEn, string TitleAr, int Attendees, int Sessions, int Speakers)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Year";
        sheet.Cell(1, 2).Value = "TitleEn";
        sheet.Cell(1, 3).Value = "TitleAr";
        sheet.Cell(1, 4).Value = "Attendees";
        sheet.Cell(1, 5).Value = "Sessions";
        sheet.Cell(1, 6).Value = "Speakers";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Year;
            sheet.Cell(i + 2, 2).Value = rows[i].TitleEn;
            sheet.Cell(i + 2, 3).Value = rows[i].TitleAr;
            sheet.Cell(i + 2, 4).Value = rows[i].Attendees;
            sheet.Cell(i + 2, 5).Value = rows[i].Sessions;
            sheet.Cell(i + 2, 6).Value = rows[i].Speakers;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateEditionAsync(string token, int year, string titleEn)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/archive",
            new CreateArchiveEditionRequest
            {
                Year = year,
                TitleEn = titleEn,
                TitleAr = "نسخة",
                Attendees = 0,
                Sessions = 0,
                Speakers = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"archive-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Archive Excel Admin",
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
