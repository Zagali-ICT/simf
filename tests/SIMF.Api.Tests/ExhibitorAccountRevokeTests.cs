// DELETE /admin/exhibitors/{id}/accounts/{membershipId} — withdrawing one
// account's booth access.
//
// Why this file exists: ExhibitorMembership was written by the provision and link
// paths and cleared by NOTHING, so an account attached to a booth kept the booth
// tools indefinitely. That row is not bookkeeping — three readers authorise on it
// (the exhibitor badge scan and the booth's captured visitor contact cards in
// ExhibitorVisitorService, the business-meeting notifications that fan out to every
// active membership, and the account count on the admin grid), so an officer who
// left the company kept reach into visitor PII until somebody retired the entire
// exhibitor.
//
// The deny test is the one that matters most. Revoke hands out and takes back
// access to visitor contact cards, so it is gated on its own
// Exhibitors.RevokeAccount rather than riding on Exhibitors.Delete; an admin
// holding every other exhibitor permission must still be refused.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ExhibitorAccountRevokeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ExhibitorAccountRevokeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Revoking_an_account_soft_deletes_its_membership_and_drops_it_from_the_booth()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorAsync();
        var (membershipId, userId) = await SeedMembershipAsync(exhibitorId);

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<ApiResult<bool?>>())!.Success);

        // Soft, not hard: the row survives because it is the attribution trail for
        // the visitor cards this account already captured, and each capture told the
        // visitor their details had been shared.
        var membership = await LoadMembershipAsync(membershipId);
        Assert.NotNull(membership);
        Assert.False(membership!.IsActive);
        Assert.NotNull(membership.DeletedAt);
        Assert.Equal(userId, membership.UserId);

        // The booth's officer list reads active memberships only, so the account is
        // gone from the surface the admin actually looks at.
        Assert.DoesNotContain(
            await ListAccountIdsAsync(exhibitorId, token),
            id => id == membershipId);
    }

    [Fact]
    public async Task Revoking_a_membership_that_belongs_to_another_exhibitor_answers_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var ownerExhibitorId = await SeedExhibitorAsync();
        var otherExhibitorId = await SeedExhibitorAsync();
        var (membershipId, _) = await SeedMembershipAsync(ownerExhibitorId);

        // The membership id is real; the exhibitor in the route is not its booth.
        // Matching on the id alone would let one exhibitor's administrator revoke
        // another's officer, so the lookup is scoped to both.
        var crossBooth = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{otherExhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.NotFound, crossBooth.StatusCode);
        Assert.Equal(
            ErrorCodes.ExhibitorAccountNotFound,
            (await crossBooth.Content.ReadFromJsonAsync<ApiResult<bool?>>())!.Error!.Code);

        // Untouched — the wrong-booth call must not revoke anything.
        Assert.True((await LoadMembershipAsync(membershipId))!.IsActive);

        var unknown = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{ownerExhibitorId}/accounts/{Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(
            ErrorCodes.ExhibitorAccountNotFound,
            (await unknown.Content.ReadFromJsonAsync<ApiResult<bool?>>())!.Error!.Code);
    }

    [Fact]
    public async Task Revoking_an_already_revoked_membership_answers_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorAsync();
        var (membershipId, _) = await SeedMembershipAsync(exhibitorId);

        var first = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Deliberately a conflict rather than an idempotent 200: an admin pressing
        // Revoke on a membership a colleague already revoked is told the access is
        // gone, instead of being left to guess whether their click did anything.
        var second = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResult<bool?>>();
        Assert.Equal(ErrorCodes.ExhibitorAccountInvalid, body!.Error!.Code);

        // Bilingual, like every other refusal this module raises.
        Assert.False(string.IsNullOrWhiteSpace(body.Error!.Message));
        Assert.False(string.IsNullOrWhiteSpace(body.Error!.MessageArabic));
    }

    [Fact]
    public async Task An_admin_without_the_revoke_permission_is_forbidden()
    {
        // Every other exhibitor permission, including Delete, and still no revoke:
        // the whole point of holding RevokeAccount separately is that retiring an
        // exhibitor and stripping one officer's reach into visitor contact cards
        // are different grants.
        var token = await CreateAdminWithCustomRoleAsync(
        [
            PermissionCatalog.Exhibitors.View,
            PermissionCatalog.Exhibitors.Create,
            PermissionCatalog.Exhibitors.Edit,
            PermissionCatalog.Exhibitors.Delete,
            PermissionCatalog.Exhibitors.LinkAccount,
        ]);
        var exhibitorId = await SeedExhibitorAsync();
        var (membershipId, _) = await SeedMembershipAsync(exhibitorId);

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // A 403 that still performed the write would be the worst of both worlds.
        Assert.True((await LoadMembershipAsync(membershipId))!.IsActive);
    }

    [Fact]
    public async Task An_admin_granted_only_the_revoke_permission_succeeds()
    {
        // The mirror of the deny test: the gate refuses on the absence of this one
        // code, not on the presence of the others. Without this, a gate wired to the
        // wrong permission could pass the deny test for the wrong reason.
        var token = await CreateAdminWithCustomRoleAsync([PermissionCatalog.Exhibitors.RevokeAccount]);
        var exhibitorId = await SeedExhibitorAsync();
        var (membershipId, _) = await SeedMembershipAsync(exhibitorId);

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await LoadMembershipAsync(membershipId))!.IsActive);
    }

    [Fact]
    public async Task An_officer_can_be_revoked_after_the_exhibitor_is_deactivated()
    {
        // Adding an officer refuses an inactive exhibitor; removing one must not.
        // Reusing the "is the booth open?" guard here would leave a closed booth's
        // officers holding their memberships with no way to strip them.
        var token = await CreateAdministratorAndSignInAsync();
        var exhibitorId = await SeedExhibitorAsync(isActive: false);
        var (membershipId, _) = await SeedMembershipAsync(exhibitorId);

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/exhibitors/{exhibitorId}/accounts/{membershipId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await LoadMembershipAsync(membershipId))!.IsActive);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> SeedExhibitorAsync(bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var exhibitorId = Guid.NewGuid();
        appDb.Exhibitors.Add(new Exhibitor
        {
            Id = exhibitorId,
            Name = $"Revoke-{Guid.NewGuid():N}"[..20],
            NameArabic = "عارض",
            IsActive = isActive,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return exhibitorId;
    }

    /// <summary>An account on the Identity database plus the App-database
    /// membership that attaches it to the booth. Two contexts and two saves, never a
    /// join: UserId is a bare Guid precisely because the two databases are
    /// physically separate.</summary>
    private async Task<(Guid MembershipId, Guid UserId)> SeedMembershipAsync(Guid exhibitorId)
    {
        var email = $"booth-officer-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Booth Officer",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var membershipId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.Set<ExhibitorMembership>().Add(new ExhibitorMembership
            {
                Id = membershipId,
                ExhibitorId = exhibitorId,
                UserId = userId,
                ContactName = string.Empty,
                RoleLabel = "Booth lead",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }

        return (membershipId, userId);
    }

    private async Task<ExhibitorMembership?> LoadMembershipAsync(Guid membershipId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await appDb.Set<ExhibitorMembership>()
            .AsNoTracking()
            .SingleOrDefaultAsync(membership => membership.Id == membershipId);
    }

    private async Task<IReadOnlyList<Guid>> ListAccountIdsAsync(Guid exhibitorId, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/exhibitors/{exhibitorId}/accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<ExhibitorAccountSummary>>>();
        return body!.Data!.Select(account => account.Id).ToList();
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"exh-revoke-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Exhibitor Revoke Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    /// <summary>An admin whose role holds exactly the codes given — the only way to
    /// test a deny path, since Administrator passes everything on the wildcard.
    /// Mirrors PermissionEnforcementTests' helper of the same name.</summary>
    private async Task<string> CreateAdminWithCustomRoleAsync(string[] grantedCodes)
    {
        var email = $"exh-revoke-limited-{Guid.NewGuid():N}@simf.test";
        var roleName = $"ExhRevokeLimited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in grantedCodes)
            {
                var definition = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == code);
                if (permission is null)
                {
                    permission = new Permission { Id = Guid.NewGuid(), Code = definition.Code };
                    db.Permissions.Add(permission);
                    await db.SaveChangesAsync();
                }
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
            await db.SaveChangesAsync();

            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Limited Exhibitor Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }
}
