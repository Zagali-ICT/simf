// The bulk-delete loop used to write its "AdminUserDeleted / Success" audit row
// INSIDE the Identity transaction, with a comment claiming that made a
// delete-without-audit pair impossible. It cannot: the operation log lives on the
// App database and the transaction is Identity-only, so a rolled-back delete left
// a durable success row that the failure row written afterwards then contradicted
// - and the EF execution strategy re-runs the whole lambda on a transient failure,
// recording one admin action as two. The row is now written after the commit.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class BulkDeleteAuditOrderingTests
    : IClassFixture<IdentityCommitFailingApiFactory>
{
    private readonly IdentityCommitFailingApiFactory _factory;
    private readonly HttpClient _client;

    public BulkDeleteAuditOrderingTests(IdentityCommitFailingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_rolled_back_delete_writes_no_success_audit_row()
    {
        var token = await CreateAdminAndSignInAsync();
        var targetId = await CreateTargetAsync();

        _factory.PoisonEnabled = true;
        HttpResponseMessage response;
        try
        {
            response = await PostAuthAsync(
                "/api/v1/admin/admins/bulk-delete",
                new AdminBulkDeleteRequest
                {
                    Ids = new List<Guid> { targetId },
                    Reason = "Bulk-delete audit-ordering regression test.",
                },
                token);
        }
        finally
        {
            _factory.PoisonEnabled = false;
        }

        // A per-target failure never explodes the batch - it counts as skipped.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(0, body.Data!.Deleted);
        Assert.Equal(1, body.Data.Skipped);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // The trail must not assert a delete that rolled back.
        var claimedDeleted = await appDb.OperationLog.AsNoTracking()
            .AnyAsync(entry => entry.SubjectUserId == targetId
                && entry.EventType == AuditEvents.AdminUserDeleted);
        Assert.False(claimedDeleted,
            "A rolled-back delete wrote a Success audit row - the audit write is "
            + "back inside the Identity transaction.");

        // The failure IS recorded, so the attempt is not invisible either.
        var recordedFailure = await appDb.OperationLog.AsNoTracking()
            .AnyAsync(entry => entry.SubjectUserId == targetId
                && entry.EventType == AuditEvents.AdminUserDeleteFailed);
        Assert.True(recordedFailure);
    }

    [Fact]
    public async Task A_committed_delete_still_writes_exactly_one_success_audit_row()
    {
        // The positive control: with the poison disarmed the runner mirrors the
        // real one, so the audit row still lands - once - after the commit.
        var token = await CreateAdminAndSignInAsync();
        var targetId = await CreateTargetAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/admins/bulk-delete",
            new AdminBulkDeleteRequest
            {
                Ids = new List<Guid> { targetId },
                Reason = "Bulk-delete audit-ordering positive control.",
            },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminBulkDeleteResponse>>())!;
        Assert.Equal(1, body.Data!.Deleted);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var successRows = await appDb.OperationLog.AsNoTracking()
            .CountAsync(entry => entry.SubjectUserId == targetId
                && entry.EventType == AuditEvents.AdminUserDeleted);
        Assert.Equal(1, successRows);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A plain Admin-typed account holding no roles, so neither the
    /// self-delete nor the Administrator-vs-Administrator guard skips it.</summary>
    private async Task<Guid> CreateTargetAsync()
    {
        var email = $"bulk-audit-target-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Bulk Audit Target",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"bulk-audit-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Bulk Audit Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
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
