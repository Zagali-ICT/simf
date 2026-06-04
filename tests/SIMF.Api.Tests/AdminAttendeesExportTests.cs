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
/// P1.6 — integration tests for the attendee-roster XLSX export
/// (<c>POST /api/v1/admin/attendees/export</c>). The export honours the same
/// <see cref="GridQuery"/> filters the list uses (incl. the CreatedAt date
/// range) and is gated by the dedicated <c>Attendees.Export</c> permission.
/// </summary>
public sealed class AdminAttendeesExportTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminAttendeesExportTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_attendees_workbook_containing_the_visitor()
    {
        var adminToken = await CreateAdminAsync();
        var visitorEmail = await SeedVisitorAsync();

        using var response = await ExportAsync(new GridQuery { Top = 5000 }, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Attendees");
        Assert.Equal("Email", sheet.Cell(1, 1).GetString());

        var lastRow = sheet.LastRowUsed()!.RowNumber();
        var emails = Enumerable.Range(2, Math.Max(0, lastRow - 1))
            .Select(r => sheet.Cell(r, 1).GetString());
        Assert.Contains(visitorEmail, emails);
    }

    [Fact]
    public async Task Export_with_a_future_from_date_returns_only_the_header_row()
    {
        var adminToken = await CreateAdminAsync();
        await SeedVisitorAsync();

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
        var sheet = workbook.Worksheet("Attendees");
        // No attendee registered on/after 2099 — only the header survives.
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<HttpResponseMessage> ExportAsync(GridQuery query, string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v1/admin/attendees/export")
        {
            Content = JsonContent.Create(query),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> CreateAdminAsync()
    {
        var email = $"att-export-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Attendee Export Admin",
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

    // A bare Visitor account — the roster excludes Admins, so the export must
    // be driven off a non-admin user. No UserProfile is needed; the roster
    // left-joins it.
    private async Task<string> SeedVisitorAsync()
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Roster Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return email;
    }
}
