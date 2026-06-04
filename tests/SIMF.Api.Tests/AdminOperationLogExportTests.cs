using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// P1.6 — integration tests for the operation-log XLSX export
/// (<c>POST /api/v1/admin/operation-log/export</c>). The export honours the
/// same <see cref="GridQuery"/> filters the list uses (incl. the date range)
/// and is gated by the dedicated <c>OperationLog.Export</c> permission.
/// </summary>
public sealed class AdminOperationLogExportTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminOperationLogExportTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_operation_log_workbook_with_rows()
    {
        var adminToken = await CreateAdminAsync();
        // Creating users writes Admin.UserCreated audit rows, so the log is
        // guaranteed non-empty by the time we export.
        await CreateRegularUserAsync(adminToken);
        await CreateRegularUserAsync(adminToken);

        using var response = await ExportAsync(new GridQuery { Top = 5000 }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("OperationLog");
        Assert.Equal("TimestampUtc", sheet.Cell(1, 1).GetString());
        Assert.Equal("EventType", sheet.Cell(1, 2).GetString());
        // Header + at least one data row.
        Assert.True(sheet.LastRowUsed()!.RowNumber() >= 2);
    }

    [Fact]
    public async Task Export_with_a_future_from_date_returns_only_the_header_row()
    {
        var adminToken = await CreateAdminAsync();
        await CreateRegularUserAsync(adminToken);

        var query = new GridQuery
        {
            Top = 5000,
            Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["from"] = "2099-01-01T00:00:00",
            },
        };
        using var response = await ExportAsync(query, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("OperationLog");
        // No row is on/after 2099 — only the header survives the filter.
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<HttpResponseMessage> ExportAsync(GridQuery query, string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/operation-log/export")
        {
            Content = JsonContent.Create(query),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> CreateAdminAsync()
    {
        var email = $"oplog-export-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "OpLog Export Admin",
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

    private async Task CreateRegularUserAsync(string adminToken)
    {
        var email = $"u-{Guid.NewGuid():N}@simf.test";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/admins");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        request.Content = JsonContent.Create(
            new AdminCreateAdminRequest { Email = email, DisplayName = "Regular User" });
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
