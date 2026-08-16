// P1.3 (D-214) — POST /api/v1/admin/admins/bulk-approve and
// POST /api/v1/admin/admins/bulk-reject. The admin-queue counterpart of the
// D-164/D-209 visitor/other bulk endpoints. Mirrors AdminBulkRejectTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminBulkAdminTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string Reason = "Duplicate admin registration — rejected during the bulk-reject test.";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminBulkAdminTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bulk_approve_admins_flips_every_subject_to_Approved()
    {
        var token = await CreateAdminAndSignInAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await CreatePendingAdminAsync());
        }

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-approve",
            new AdminBulkApprovalRequest { Ids = ids },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkApprovalResponse>>())!.Data!;
        Assert.Equal(3, body.Approved);
        Assert.Equal(0, body.Skipped);
        Assert.Empty(body.Failures);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        foreach (var id in ids)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
            Assert.Equal(AccountState.Approved, user.AccountState);
        }
    }

    [Fact]
    public async Task Bulk_reject_admins_flips_every_subject_to_Rejected()
    {
        var token = await CreateAdminAndSignInAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await CreatePendingAdminAsync());
        }

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-reject",
            new AdminBulkRejectRequest { Ids = ids, Reason = Reason },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkRejectResponse>>())!.Data!;
        Assert.Equal(3, body.Rejected);
        Assert.Equal(0, body.Skipped);
        Assert.Empty(body.Failures);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        foreach (var id in ids)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
            Assert.Equal(AccountState.Rejected, user.AccountState);
        }
    }

    [Fact]
    public async Task Bulk_reject_admins_empty_ids_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-reject",
            new AdminBulkRejectRequest { Ids = new List<Guid>(), Reason = Reason },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_approve_admins_non_admin_caller_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-approve",
            new AdminBulkApprovalRequest { Ids = new List<Guid> { Guid.NewGuid() } },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreatePendingAdminAsync()
    {
        var email = $"pending-admin-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Bulk Admin Subject",
            AccountState = AccountState.PendingApproval,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"bulk-admin-actor-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Bulk Admin Actor",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
