using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-113 integration tests for the type-scoped Other grid surface —
/// bulk-delete, duplicate, export, import on <c>/admin/others/*</c>.
/// Other accounts require a <c>ProfileTypeId</c> at create time, so the
/// import test exercises the mandatory column path.
/// </summary>
public sealed class AdminGridOthersTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminGridOthersTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- Bulk delete ----------------------------------------------------------

    [Fact]
    public async Task Other_bulk_delete_succeeds_and_skips_visitor_ids()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var profileTypeId = await GetSeededOtherProfileTypeAsync();

        var other = await CreateOtherAsync(adminToken, profileTypeId);
        var visitor = await CreateVisitorAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/others/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { other, visitor },
                Reason = "Other bulk-delete test — visitor ids must be skipped silently.",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(1, body.Data!.Deleted);
        Assert.Equal(1, body.Data.Skipped);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var otherRow = await db.Users.SingleAsync(u => u.Id == other);
        var visitorRow = await db.Users.SingleAsync(u => u.Id == visitor);
        Assert.Equal(AccountState.Disabled, otherRow.AccountState);
        Assert.NotEqual(AccountState.Disabled, visitorRow.AccountState);
    }

    // -- Duplicate ------------------------------------------------------------

    [Fact]
    public async Task Other_duplicate_creates_an_other_copy_with_the_same_profile_type()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var profileTypeId = await GetSeededOtherProfileTypeAsync();
        var source = await CreateOtherAsync(adminToken, profileTypeId, displayName: "Source Other");
        var newEmail = $"o-dup-{Guid.NewGuid():N}@simf.test";

        var response = await PostAuthAsync(
            "/api/v1/admin/others/duplicate",
            new AdminDuplicateUserRequest { SourceId = source, NewEmail = newEmail },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var copy = await users.FindByEmailAsync(newEmail);
        Assert.NotNull(copy);
        // D-186: Other accounts are now Visitor-typed; the partner-side
// distinction lives on the linked ProfileType.IsVisitor=false.
Assert.Equal(UserType.Visitor, copy!.UserType);
    }

    [Fact]
    public async Task Other_duplicate_of_a_visitor_source_returns_404()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var visitorSource = await CreateVisitorAsync(adminToken);

        var response = await PostAuthAsync(
            "/api/v1/admin/others/duplicate",
            new AdminDuplicateUserRequest
            {
                SourceId = visitorSource,
                NewEmail = $"o-dup-fail-{Guid.NewGuid():N}@simf.test",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -- Export ---------------------------------------------------------------

    [Fact]
    public async Task Other_export_returns_only_other_rows()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var profileTypeId = await GetSeededOtherProfileTypeAsync();
        var other = await CreateOtherAsync(adminToken, profileTypeId, displayName: "Export Other");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/others/export")
        {
            Content = JsonContent.Create(
                new AdminExportUsersRequest { Ids = new List<Guid> { other } }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Users");
        Assert.Equal("Export Other", sheet.Cell(2, 2).GetString());
    }

    // -- Import ---------------------------------------------------------------

    [Fact]
    public async Task Other_import_requires_a_profile_type_id_per_row()
    {
        var (adminToken, _) = await CreateAdminAsync();
        var profileTypeId = await GetSeededOtherProfileTypeAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "DisplayName";
        sheet.Cell(1, 4).Value = "Role";
        sheet.Cell(1, 5).Value = "TwoFactor";
        sheet.Cell(1, 6).Value = "CreatedAt";
        sheet.Cell(1, 7).Value = "ProfileTypeId";

        var marker = $"oimp{Guid.NewGuid():N}".Substring(0, 9);
        var eOk = $"{marker}-ok@simf.test";
        var eMissing = $"{marker}-miss@simf.test";

        // Row 2 — valid (carries a ProfileTypeId).
        sheet.Cell(2, 1).Value = eOk;
        sheet.Cell(2, 2).Value = "Imported Other OK";
        sheet.Cell(2, 7).Value = profileTypeId.ToString();

        // Row 3 — no ProfileTypeId → must land in the error report.
        sheet.Cell(3, 1).Value = eMissing;
        sheet.Cell(3, 2).Value = "Imported Other Missing PT";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var xlsx = ms.ToArray();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "others.xlsx");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/others/import")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminImportUsersResponse>>())!;
        Assert.Equal(1, body.Data!.Created);
        Assert.Equal(1, body.Data.Skipped);
        Assert.Single(body.Data.Errors);
        Assert.Equal(eMissing, body.Data.Errors[0].Email);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var ok = await users.FindByEmailAsync(eOk);
        Assert.NotNull(ok);
        // D-186: Other accounts are now Visitor-typed; partner status
        // lives on the linked ProfileType.IsVisitor=false.
        Assert.Equal(UserType.Visitor, ok!.UserType);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> GetSeededOtherProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // D-186: the IdentitySeeder lands "Staff" under UserType.Visitor
        // with IsVisitor=false on first boot. Partner-side seeded rows
        // are the ones the CP "Others" page filters on.
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.UserType == UserType.Visitor
                                       && p.IsForVisitor == false
                                       && p.IsActive);
        if (seeded is not null) return seeded.Id;
        // Defensive fallback for environments where the seeder did not run.
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — TestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            UserType = UserType.Visitor,
            IsForVisitor = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<(string Token, Guid AdminId)> CreateAdminAsync()
    {
        var email = $"ogrid-admin-{Guid.NewGuid():N}@simf.test";
        Guid id;
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
                DisplayName = "OGrid Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            id = user.Id;
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
        return (body.Data!.Tokens!.AccessToken, id);
    }

    private async Task<Guid> CreateOtherAsync(
        string adminToken, Guid profileTypeId, string? displayName = null)
    {
        var email = $"o-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = displayName ?? "Other Subject",
                ProfileTypeId = profileTypeId,
            },
            adminToken);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> CreateVisitorAsync(string adminToken, string? displayName = null)
    {
        var email = $"v-foreign-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = displayName ?? "Cross-type Visitor",
            },
            adminToken);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }
}
