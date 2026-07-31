// D-209 — POST /api/v1/admin/visitors/bulk-reject and
// POST /api/v1/admin/others/bulk-reject. The reject counterpart of the
// D-164 bulk-approve "Select All" affordance.
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
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminBulkRejectTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string Reason = "Duplicate registration — rejected during the bulk-reject test.";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminBulkRejectTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Bulk_reject_visitors_flips_every_subject_to_Rejected_and_records_the_reason()
    {
        var token = await CreateAdminAndSignInAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await CreatePendingVisitorAsync());
        }

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-reject",
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
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        foreach (var id in ids)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
            Assert.Equal(AccountState.Rejected, user.AccountState);

            // D-106 — the reason is persisted on the profile (EN mirrored to AR).
            var profile = await appDb.UserProfiles.AsNoTracking()
                .SingleAsync(p => p.UserId == id);
            Assert.Equal(Reason, profile.RejectionReason);
        }
    }

    [Fact]
    public async Task Bulk_reject_with_unknown_id_records_a_failure_and_continues()
    {
        var token = await CreateAdminAndSignInAsync();
        var goodId = await CreatePendingVisitorAsync();
        var unknownId = Guid.NewGuid();

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-reject",
            new AdminBulkRejectRequest
            {
                Ids = new List<Guid> { goodId, unknownId },
                Reason = Reason,
            },
            token);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminBulkRejectResponse>>())!.Data!;
        Assert.Equal(1, body.Rejected);
        Assert.Equal(1, body.Skipped);
        Assert.Single(body.Failures);
        Assert.Equal(unknownId, body.Failures[0].UserId);
    }

    [Fact]
    public async Task Empty_ids_array_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-reject",
            new AdminBulkRejectRequest { Ids = new List<Guid>(), Reason = Reason },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Short_reason_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreatePendingVisitorAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-reject",
            new AdminBulkRejectRequest { Ids = new List<Guid> { id }, Reason = "too short" },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/bulk-reject",
            new AdminBulkRejectRequest
            {
                Ids = new List<Guid> { Guid.NewGuid() },
                Reason = Reason,
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreatePendingVisitorAsync()
    {
        var email = $"pending-rej-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Bulk Reject Visitor",
            AccountState = AccountState.PendingApproval,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            NameArabic = "زائر تجريبي",
            Name = "Bulk Reject Visitor",
            NationalityId = 682,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return user.Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"bulk-rej-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Bulk Reject Admin",
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
