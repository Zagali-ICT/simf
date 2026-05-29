using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-124 integration tests for the scoped pending-profile read endpoints
/// (<c>GET /admin/visitors/{id}/profile-for-approval</c> and
/// <c>GET /admin/others/{id}/profile-for-approval</c>). Confirms the
/// single-404-for-all-mismatch policy that closes the enumeration hole:
/// unknown id, approved target, and wrong-type id all return the same
/// 404 + ErrorCodes.NotFound.
/// </summary>
public sealed class PendingProfileReadTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public PendingProfileReadTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_read_a_pending_visitor_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken, displayName: "Pending Visitor");

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!;
        Assert.NotNull(body.Data);
        Assert.Equal(visitor, body.Data!.Id);
        Assert.Equal("Visitor", body.Data.UserType);
        Assert.Equal("Pending Visitor", body.Data.DisplayName);
        // An admin-created visitor has no profile row yet — the response carries the
        // identity fields plus an empty interest list and HasIdImage=false.
        Assert.Empty(body.Data.InterestIds);
        Assert.False(body.Data.HasIdImage);
    }

    [Fact]
    public async Task Admin_can_read_a_pending_other_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        var response = await GetAuthAsync(
            $"/api/v1/admin/others/{otherId}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<PendingProfileResponse>>())!;
        Assert.NotNull(body.Data);
        Assert.Equal("Other", body.Data!.UserType);
    }

    [Fact]
    public async Task Reading_an_approved_visitor_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        // Approve so the AccountState moves out of PendingApproval — the
        // read endpoint must then deny because it is scoped to Pending.
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.NotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Reading_a_pending_visitor_via_others_endpoint_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        // Wrong type — the /others/.../profile-for-approval endpoint must
        // refuse a Visitor id with the same 404 a missing id would emit.
        var response = await GetAuthAsync(
            $"/api/v1/admin/others/{visitor}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_pending_other_via_visitors_endpoint_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var otherId = await CreatePendingOtherAsync(adminToken);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{otherId}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_an_unknown_id_returns_404()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}/profile-for-approval", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_non_admin_caller_is_forbidden()
    {
        var visitorTokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await GetAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}/profile-for-approval",
            visitorTokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_read_does_not_mutate_account_state()
    {
        // Spec C item (g) intended a row-audit assertion on read — but
        // D-109's SaveChanges interceptor only fires on writes, so a
        // pure read never produces a RowAudit row. This replacement
        // case proves the read is non-destructive (AccountState stays
        // PendingApproval after the read), which is the behaviour the
        // approve / reject flow depends on.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitor = await CreatePendingVisitorAsync(adminToken);

        var read = await GetAuthAsync(
            $"/api/v1/admin/visitors/{visitor}/profile-for-approval", adminToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.Users.SingleAsync(u => u.Id == visitor);
        Assert.Equal(AccountState.PendingApproval, row.AccountState);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreatePendingVisitorAsync(
        string adminToken, string? displayName = null)
    {
        var email = $"pending-v-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = displayName ?? "Pending Visitor Subject",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> CreatePendingOtherAsync(string adminToken)
    {
        var profileTypeId = await GetSeededOtherProfileTypeAsync();
        var email = $"pending-o-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = "Pending Other Subject",
                ProfileTypeId = profileTypeId,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminCreateUserResponse>>())!;
        return body.Data!.UserId;
    }

    private async Task<Guid> GetSeededOtherProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.UserType == UserType.Other && p.IsActive);
        if (seeded is not null) { return seeded.Id; }
        var fresh = new ProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — PendingReadTestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            UserType = UserType.Other,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"pending-read-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Pending Read Tests Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = Password,
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
        string url, TBody? body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return _client.SendAsync(request);
    }
}
