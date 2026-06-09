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
/// Integration tests for the D-356 generic grid Excel engine, exercised through
/// the Countries resource: export round-trip, a positive import, the
/// upload-defence rejections (not-a-workbook, wrong sheet) and the Export
/// permission gate.
/// <para>Country's key is an int (ISO 3166-1 numeric) that the caller assigns
/// and the service requires unique, so each test mints fresh ids/codes to keep
/// the shared test database deterministic.</para>
/// </summary>
public sealed class CountriesExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public CountriesExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateCountryAsync(adminToken, FreshId(), FreshCode(), "Export A");
        await CreateCountryAsync(adminToken, FreshId(), FreshCode(), "Export B");

        var response = await PostAuthAsync(
            "/api/v1/admin/countries/export",
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
        var idOne = FreshId();
        var idTwo = FreshId();
        var codeOne = FreshCode();
        var codeTwo = FreshCode();
        var nameOne = $"Imported One {Guid.NewGuid():N}";
        var nameTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildCountriesWorkbook("Countries",
            (idOne, codeOne, nameOne, "مستورد ١", "+11", 3),
            (idTwo, codeTwo, nameTwo, "مستورد ٢", "+12", 4));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/countries/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/countries/list", new GridQuery { Top = 500 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminCountrySummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Code == codeOne);
        Assert.Contains(page.Items, item => item.Code == codeTwo);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_duplicate_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existingId = FreshId();
        var existingCode = FreshCode();
        await CreateCountryAsync(adminToken, existingId, existingCode, "Existing");
        var freshCode = FreshCode();

        // One duplicate row (must error) + one new row (must still be created).
        var workbook = BuildCountriesWorkbook("Countries",
            (existingId, existingCode, "Duplicate", "مكرر", "+11", 1),
            (FreshId(), freshCode, "Fresh", "جديد", "+12", 2));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/countries/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(existingCode, result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/countries/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildCountriesWorkbook("NotCountries",
            (FreshId(), FreshCode(), "X", "س", "+11", 1));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/countries/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/countries/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    // The seed (CountryConfiguration) holds ~56 real ISO countries up to id 887,
    // so mint ids from 900 — ISO 3166-1 numeric never exceeds 894, leaving
    // 900..998 free (the same user-assigned range AdminCountriesTests uses).
    private static int _nextId = 900;
    private static int FreshId() => Interlocked.Increment(ref _nextId);

    // A unique 2-char code shaped letter+digit (e.g. "A0"): the service only
    // checks length 2 + uniqueness, and a letter+digit code can never collide
    // with a real ISO alpha-2 (letter+letter) seed row. 26*10 = 260 codes.
    private static int _nextCode = -1;
    private static string FreshCode()
    {
        var n = Interlocked.Increment(ref _nextCode);
        return $"{(char)('A' + (n / 10 % 26))}{(char)('0' + (n % 10))}";
    }

    private static byte[] BuildCountriesWorkbook(
        string sheetName,
        params (int Id, string Code, string Name, string NameArabic, string? PhonePrefix, int DisplayOrder)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Id";
        sheet.Cell(1, 2).Value = "Code";
        sheet.Cell(1, 3).Value = "Name";
        sheet.Cell(1, 4).Value = "NameArabic";
        sheet.Cell(1, 5).Value = "PhonePrefix";
        sheet.Cell(1, 6).Value = "DisplayOrder";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Id;
            sheet.Cell(i + 2, 2).Value = rows[i].Code;
            sheet.Cell(i + 2, 3).Value = rows[i].Name;
            sheet.Cell(i + 2, 4).Value = rows[i].NameArabic;
            sheet.Cell(i + 2, 5).Value = rows[i].PhonePrefix;
            sheet.Cell(i + 2, 6).Value = rows[i].DisplayOrder;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateCountryAsync(string token, int id, string code, string name)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/countries",
            new AdminCreateCountryRequest
            {
                Id = id,
                Code = code,
                Name = name,
                NameArabic = "بلد",
                PhonePrefix = "+1",
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"country-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Country Excel Admin",
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
