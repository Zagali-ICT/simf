using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 meeting-tables grid export (SIMF-FDS-013).
/// Meeting tables are hall-scoped and defined / generated from the page's own
/// modals, so the resource is export-only — this covers the export round-trip
/// for a seeded hall + table and the Export permission gate (non-admin → 403).
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class MeetingTablesExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingTablesExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallId = await CreateHallAsync(adminToken);
        // A fresh hall defaults to HallPurpose.General, which accepts tables.
        await CreateTableAsync(adminToken, hallId, NewCode());

        var response = await PostAuthAsync(
            "/api/v1/admin/meeting-tables/export",
            new AdminGridExportRequest
            {
                Query = new GridQuery
                {
                    Top = 100,
                    Filters = new Dictionary<string, string> { ["hallId"] = hallId.ToString() },
                },
            },
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
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/meeting-tables/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    // A unique 2..16-char code (the create service upper-cases + requires
    // per-hall uniqueness); 12 hex chars keeps every generated code distinct.
    private static string NewCode() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private async Task<Guid> CreateHallAsync(string token)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = NewCode(),
                Name = $"Meeting Tables Export {Guid.NewGuid():N}",
                NameArabic = "قاعة",
                Capacity = 100,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminHallDetail>>())!;
        return body.Data!.Id;
    }

    private async Task CreateTableAsync(string token, Guid hallId, string code)
    {
        var response = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = code, Capacity = 4 },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"meeting-tables-xlsx-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Meeting Tables Excel Admin",
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
}
