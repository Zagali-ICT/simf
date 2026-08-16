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
/// ContentBlocks (an upsert-by-key resource): export round-trip, a positive
/// import that creates new rows, an import whose existing key reports Updated
/// (not Created) without aborting the batch, a per-row error for a blank
/// required field, the upload-defence rejections (not-a-workbook, wrong sheet)
/// and the Export permission gate.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ContentBlocksExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ContentBlocksExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await UpsertContentBlockAsync(adminToken, NewKey(), $"Export A {Guid.NewGuid():N}");
        await UpsertContentBlockAsync(adminToken, NewKey(), $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/content-blocks/export",
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
    public async Task Import_creates_each_new_row_and_reports_the_outcome()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var keyOne = NewKey();
        var keyTwo = NewKey();
        var contentOne = $"Imported One {Guid.NewGuid():N}";
        var contentTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildContentBlocksWorkbook("ContentBlocks",
            (keyOne, contentOne, "مستورد ١", true),
            (keyTwo, contentTwo, "مستورد ٢", true));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/content-blocks/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/content-blocks/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminContentBlockSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Key == keyOne);
        Assert.Contains(page.Items, item => item.Key == keyTwo);
    }

    [Fact]
    public async Task Import_reports_an_existing_key_as_updated_not_created()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var existing = NewKey();
        await UpsertContentBlockAsync(adminToken, existing, "Original");
        var fresh = NewKey();

        // One existing key (must report Updated) + one new key (must report Created).
        var workbook = BuildContentBlocksWorkbook("ContentBlocks",
            (existing, $"Changed {Guid.NewGuid():N}", "محدث", true),
            (fresh, $"Fresh {Guid.NewGuid():N}", "جديد", true));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/content-blocks/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_a_blank_required_field_without_aborting()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var blankContentKey = NewKey();
        var good = NewKey();

        // One row with blank Content (must error) + one valid row (must be created).
        var workbook = BuildContentBlocksWorkbook("ContentBlocks",
            (blankContentKey, string.Empty, "بدون محتوى", true),
            (good, $"Good {Guid.NewGuid():N}", "جيد", true));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/content-blocks/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(1, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(blankContentKey, result.Errors[0].Key);
    }

    [Fact]
    public async Task Import_rejects_a_file_that_is_not_a_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var notXlsx = "this is plain text, not a zip"u8.ToArray();

        var response = await PostFileAuthAsync(
            "/api/v1/admin/content-blocks/import", notXlsx, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_rejects_a_workbook_with_the_wrong_sheet_name()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var wrongSheet = BuildContentBlocksWorkbook("NotContentBlocks",
            (NewKey(), $"X {Guid.NewGuid():N}", "س", true));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/content-blocks/import", wrongSheet, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/content-blocks/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    /// <summary>A unique lower-case content-block key (the service normalises the
    /// key to lower-case; we generate within that form so the echoed key matches
    /// the input).</summary>
    private static string NewKey() => "test." + Guid.NewGuid().ToString("N");

    private static byte[] BuildContentBlocksWorkbook(
        string sheetName,
        params (string Key, string Content, string ContentArabic, bool IsActive)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Key";
        sheet.Cell(1, 2).Value = "Content";
        sheet.Cell(1, 3).Value = "ContentArabic";
        sheet.Cell(1, 4).Value = "IsActive";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Key;
            sheet.Cell(i + 2, 2).Value = rows[i].Content;
            sheet.Cell(i + 2, 3).Value = rows[i].ContentArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].IsActive;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task UpsertContentBlockAsync(string token, string key, string content)
    {
        var response = await PutAuthAsync(
            "/api/v1/admin/content-blocks",
            new UpsertContentBlockRequest
            {
                Key = key,
                Content = content,
                ContentArabic = "محتوى",
                IsActive = true,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"content-block-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test ContentBlock Excel Admin",
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
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
