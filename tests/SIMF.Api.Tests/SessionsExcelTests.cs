using System.Globalization;
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
/// Integration tests for the D-356 Sessions grid Excel engine: the export
/// round-trip (ZIP magic), the Export permission gate, and an insert-only import
/// round-trip keyed on the session code (resolving the mandatory Hall foreign key
/// from its code). Test data carries Guid-unique codes so it is robust on the
/// non-reset integration DB (the session code is the create de-dup key).
/// </summary>
public sealed class SessionsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        await CreateSessionAsync(adminToken, UniqueCode(), hallCode);
        await CreateSessionAsync(adminToken, UniqueCode(), hallCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
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
        var hallCode = await CreateHallAsync(adminToken);
        var codeOne = UniqueCode();
        var codeTwo = UniqueCode();
        var start = "2026-01-30T09:00:00Z";
        var end = "2026-01-30T10:30:00Z";
        var workbook = BuildSessionsWorkbook("Sessions",
            (codeOne, "Session One", "جلسة ١", hallCode, start, end),
            (codeTwo, "Session Two", "جلسة ٢", hallCode, start, end));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 2);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Code == codeOne);
        Assert.Contains(page.Items, item => item.Code == codeTwo);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_an_unknown_hall()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = UniqueCode();
        var workbook = BuildSessionsWorkbook("Sessions",
            (code, "Orphan", "يتيمة", "NO-SUCH-HALL",
                "2026-01-30T09:00:00Z", "2026-01-30T10:00:00Z"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        // The unresolved hall is a per-row error, not a batch abort.
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(code, result.Errors[0].Key);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    // A unique session/hall code within the 2–16 char / uppercase rule (the
    // create de-dup key) so the test is robust on the non-reset integration DB.
    private static string UniqueCode() =>
        ("S" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    private static byte[] BuildSessionsWorkbook(
        string sheetName,
        params (string Code, string Title, string TitleArabic, string Hall,
            string StartUtc, string EndUtc)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Title";
        sheet.Cell(1, 3).Value = "TitleArabic";
        sheet.Cell(1, 4).Value = "Hall";
        sheet.Cell(1, 5).Value = "StartUtc";
        sheet.Cell(1, 6).Value = "EndUtc";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Title;
            sheet.Cell(i + 2, 3).Value = rows[i].TitleArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].Hall;
            sheet.Cell(i + 2, 5).Value = rows[i].StartUtc;
            sheet.Cell(i + 2, 6).Value = rows[i].EndUtc;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Creates an active hall and returns its (unique) code so the imported /
    // created sessions can reference it by its natural key.
    private async Task<string> CreateHallAsync(string token)
    {
        var code = UniqueCode();
        var response = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = code,
                Name = $"Hall {code}",
                NameArabic = $"قاعة {code}",
                Capacity = 200,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return code;
    }

    private async Task CreateSessionAsync(string token, string code, string hallCode)
    {
        // Resolve the hall id from its code (the create request takes the id).
        var list = await PostAuthAsync(
            "/api/v1/admin/halls/list",
            new GridQuery { Top = 500, Search = hallCode },
            token);
        var halls = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminHallSummary>>>())!.Data!;
        var hallId = halls.Items.First(h =>
            string.Equals(h.Code, hallCode, StringComparison.OrdinalIgnoreCase)).Id;

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code,
                Title = $"Session {code}",
                TitleArabic = $"جلسة {code}",
                HallId = hallId,
                StartUtc = DateTimeOffset.Parse(
                    "2026-01-30T09:00:00Z", CultureInfo.InvariantCulture),
                EndUtc = DateTimeOffset.Parse(
                    "2026-01-30T10:00:00Z", CultureInfo.InvariantCulture),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"session-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Session Excel Admin",
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
