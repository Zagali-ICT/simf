// Organisation lookup module — admin CRUD + gov Excel import + public picker search.
// Mirrors AdminBoothsTests and the AdminGridV2 import flow.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Organisations;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Integration tests for the Organisation lookup module: admin CRUD,
/// the gov Excel upsert import, and the public picker search.</summary>
public sealed class OrganisationTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public OrganisationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_then_get_then_list_contains_the_organisation()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var cr = NewCr();
        var create = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest
            {
                NameAr = "الصناعات العسكرية السعودية",
                NameEn = "Saudi Arabian Military Industries",
                CommercialRegistration = cr,
                Sector = "Defense",
                City = "Riyadh",
                Phone = "+966112000000",
                Email = "info@sami.com.sa",
                Website = "https://sami.com.sa",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminOrganisationDetail>>())!.Data!;
        Assert.Equal(cr, created.CommercialRegistration);
        Assert.Equal("Saudi Arabian Military Industries", created.NameEn);
        Assert.True(created.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/organisations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminOrganisationDetail>>())!.Data!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Riyadh", fetched.City);

        var list = await PostAuthAsync(
            "/api/v1/admin/organisations/list",
            new GridQuery { Top = 200 },
            token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminOrganisationSummary>>>())!.Data!;
        Assert.Contains(page.Items, o => o.Id == created.Id);
    }

    [Fact]
    public async Task Duplicate_commercial_registration_is_a_409_ORGANISATION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var cr = NewCr();
        var first = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest { NameAr = "شركة أولى", CommercialRegistration = cr },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest { NameAr = "شركة ثانية", CommercialRegistration = cr },
            token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OrganisationInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/organisations/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_marks_the_organisation_inactive()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var create = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest { NameAr = "شركة للحذف", CommercialRegistration = NewCr() },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminOrganisationDetail>>())!.Data!;

        var delete = await DeleteAuthAsync($"/api/v1/admin/organisations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/organisations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminOrganisationDetail>>())!.Data!;
        Assert.False(fetched.IsActive);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest { NameAr = "شركة الزائر", CommercialRegistration = NewCr() },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Import_inserts_new_rows_and_re_importing_upserts_by_commercial_registration()
    {
        var token = await CreateAdministratorAndSignInAsync();

        var cr1 = NewCr();
        var cr2 = NewCr();
        var xlsx = BuildWorkbook(
            ("منظمة الاستيراد الأولى", "Import Org One", cr1),
            ("منظمة الاستيراد الثانية", "Import Org Two", cr2));

        // First import: both rows are new inserts.
        var first = await ImportAsync(xlsx, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = (await first.Content
            .ReadFromJsonAsync<ApiResult<OrganisationImportResult>>())!.Data!;
        Assert.Equal(2, firstResult.RowsRead);
        Assert.Equal(2, firstResult.Inserted);
        Assert.Equal(0, firstResult.Updated);

        // Re-importing the same file is an idempotent upsert keyed by CR.
        var second = await ImportAsync(xlsx, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondResult = (await second.Content
            .ReadFromJsonAsync<ApiResult<OrganisationImportResult>>())!.Data!;
        Assert.Equal(2, secondResult.RowsRead);
        Assert.Equal(0, secondResult.Inserted);
        Assert.Equal(2, secondResult.Updated);
    }

    [Fact]
    public async Task Public_picker_search_returns_the_matching_organisation()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var marker = $"Pick{Guid.NewGuid():N}".Substring(0, 8);
        var nameEn = $"{marker} Maritime Logistics";
        var create = await PostAuthAsync(
            "/api/v1/admin/organisations",
            new CreateOrganisationRequest
            {
                NameAr = "الخدمات اللوجستية البحرية",
                NameEn = nameEn,
                CommercialRegistration = NewCr(),
                City = "Jeddah",
            },
            token);
        var created = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminOrganisationDetail>>())!.Data!;

        // The picker search is authenticated (signed-in user completing their
        // profile); it is deliberately NOT anonymous, so pass the bearer token.
        var search = await GetAuthAsync($"/api/v1/app/organisations?search={marker}&top=20", token);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var items = (await search.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<OrganisationPickerItem>>>())!.Data!;
        Assert.Contains(items, item => item.Id == created.Id);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewCr() => Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static byte[] BuildWorkbook(params (string NameAr, string NameEn, string Cr)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Organisations");
        sheet.Cell(1, 1).Value = "NameAr";
        sheet.Cell(1, 2).Value = "NameEn";
        sheet.Cell(1, 3).Value = "CommercialRegistration";
        var row = 2;
        foreach (var (nameAr, nameEn, cr) in rows)
        {
            sheet.Cell(row, 1).Value = nameAr;
            sheet.Cell(row, 2).Value = nameEn;
            sheet.Cell(row, 3).Value = cr;
            row++;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private Task<HttpResponseMessage> ImportAsync(byte[] xlsx, string token)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "organisations.xlsx");

        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/organisations/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"org-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Organisation Admin",
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
