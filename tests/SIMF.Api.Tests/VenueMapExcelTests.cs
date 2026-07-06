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
using SIMF.Domain.Venue;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 venue-map grid Excel engine: the export
/// round-trip (ZIP magic), the Export permission gate, and an insert-only import
/// round-trip that maps the Kind display name back to its enum value. The
/// optional Hall / Booth FK columns are left blank here (they are resolved by a
/// human-readable code; a blank code leaves the link unset), so the round-trip
/// needs no hall / booth fixtures.
/// </summary>
public sealed class VenueMapExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public VenueMapExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        await CreateNodeAsync(adminToken, $"Export A {Guid.NewGuid():N}");
        await CreateNodeAsync(adminToken, $"Export B {Guid.NewGuid():N}");

        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map/export",
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
        var labelOne = $"Imported One {Guid.NewGuid():N}";
        var labelTwo = $"Imported Two {Guid.NewGuid():N}";
        var workbook = BuildVenueMapWorkbook("VenueMap",
            (labelOne, "عقدة ١", "Zone", 1.5, 2.5),
            (labelTwo, "عقدة ٢", "PointOfInterest", 3.0, 4.0));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/venue-map/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 2);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/venue-map/list", new GridQuery { Top = 500 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminVenueMapNodeSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Label == labelOne);
        Assert.Contains(page.Items, item => item.Label == labelTwo);
    }

    [Fact]
    public async Task Import_records_a_per_row_error_for_an_unknown_kind()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var bad = $"Bad Kind {Guid.NewGuid():N}";
        var good = $"Good Kind {Guid.NewGuid():N}";
        var workbook = BuildVenueMapWorkbook("VenueMap",
            (bad, "عقدة سيئة", "NotAKind", 0, 0),
            (good, "عقدة جيدة", "Hall", 0, 0));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/venue-map/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        // The bad row errors but the good one still imports (batch not aborted).
        Assert.True(result.Created >= 1);
        Assert.Contains(result.Errors, error => error.Key == bad);
    }

    [Fact]
    public async Task Export_returns_every_row_past_the_list_page_cap()
    {
        // Regression for D-642: the whole-grid export requested every row, but the
        // list service re-clamped Top back to its page size — silently truncating
        // any grid larger than one page. The venue-map list clamps at 500
        // (ClampPage(50, 500)), so seed 600 uniquely-labelled nodes: pre-fix only
        // 500 came back; the fix pages through and returns all 600.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var prefix = $"Bulk-{Guid.NewGuid():N}";
        const int seeded = 600;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            for (var i = 0; i < seeded; i++)
            {
                db.VenueMapNodes.Add(new VenueMapNode
                {
                    Id = Guid.NewGuid(),
                    Label = $"{prefix}-{i:D3}",
                    LabelArabic = $"عقدة {i}",
                    Kind = VenueMapNodeKind.Zone,
                    X = i,
                    Y = i,
                });
            }
            await db.SaveChangesAsync();
        }

        // The client asks for a 50-row page; the export pages through the whole set
        // regardless of the client Top, so all 600 must still come back.
        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 50 } },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("VenueMap");
        // Column 1 is Label; row 1 is the header (excluded by the prefix filter).
        // Count only the rows we seeded so the assertion is independent of any
        // nodes the other tests in this class add.
        var exported = sheet.Column(1).CellsUsed()
            .Select(cell => cell.GetString())
            .Count(label => label.StartsWith(prefix, StringComparison.Ordinal));

        Assert.Equal(seeded, exported);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private static byte[] BuildVenueMapWorkbook(
        string sheetName,
        params (string Label, string LabelArabic, string Kind, double X, double Y)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Label";
        sheet.Cell(1, 2).Value = "LabelArabic";
        sheet.Cell(1, 3).Value = "Kind";
        sheet.Cell(1, 4).Value = "X";
        sheet.Cell(1, 5).Value = "Y";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Label;
            sheet.Cell(i + 2, 2).Value = rows[i].LabelArabic;
            sheet.Cell(i + 2, 3).Value = rows[i].Kind;
            sheet.Cell(i + 2, 4).Value = rows[i].X;
            sheet.Cell(i + 2, 5).Value = rows[i].Y;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task CreateNodeAsync(string token, string label)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/venue-map",
            new AdminCreateVenueMapNodeRequest
            {
                Label = label,
                LabelArabic = $"عقدة {Guid.NewGuid():N}",
                Kind = VenueMapNodeKind.Hall,
                X = 0,
                Y = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"venue-map-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Venue Map Excel Admin",
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
