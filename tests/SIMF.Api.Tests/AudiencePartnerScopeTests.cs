// The Visitors and Others desks share one account pool (UserType.Visitor); only
// the linked ProfileType tells them apart. "Others" is exactly the accounts linked
// to a partner-side ProfileType; "Visitors" is EVERYTHING ELSE in that pool -
// including a self-signed-up visitor whose profile row carries no ProfileType at
// all, which is the case an earlier implementation dropped from both queues.
//
// That complement used to be computed by reading every Visitor id out of the
// Identity database and subtracting the partner set in memory. It is now expressed
// as a NOT IN against the (small) partner set, pushed into SQL. These tests pin the
// membership rule itself, so the two forms cannot silently disagree.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AudiencePartnerScopeTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AudiencePartnerScopeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task The_two_desks_split_the_visitor_pool_with_no_overlap_and_no_gap()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);
        var audienceTypeId = await SeedProfileTypeAsync(isForVisitor: true);

        var partnerEmail = await CreateOtherAsync(token, partnerTypeId);
        var tieredEmail = await CreateVisitorAsync(token, audienceTypeId);
        // No ProfileType at all - the self-signup shape. It belongs to the
        // audience desk, and to neither desk under the old "minus every profile"
        // rule this replaced.
        var untieredEmail = await CreateVisitorAsync(token, profileTypeId: null);

        var visitors = await ListEmailsAsync(token, "/api/v1/admin/visitors/list");
        Assert.Contains(tieredEmail, visitors);
        Assert.Contains(untieredEmail, visitors);
        Assert.DoesNotContain(partnerEmail, visitors);

        var others = await ListEmailsAsync(token, "/api/v1/admin/others/list");
        Assert.Contains(partnerEmail, others);
        Assert.DoesNotContain(tieredEmail, others);
        Assert.DoesNotContain(untieredEmail, others);
    }

    [Fact]
    public async Task The_two_pending_queues_split_the_same_way()
    {
        // The queues run the same scope resolution over a PendingApproval-narrowed
        // query. An admin-created account lands PendingApproval, so all three rows
        // are queue members from the moment they exist.
        var token = await CreateAdministratorAndSignInAsync();
        var partnerTypeId = await SeedProfileTypeAsync(isForVisitor: false);

        var partnerEmail = await CreateOtherAsync(token, partnerTypeId);
        var untieredEmail = await CreateVisitorAsync(token, profileTypeId: null);

        var pendingVisitors = await ListPendingEmailsAsync(
            token, "/api/v1/admin/visitors/pending/list");
        Assert.Contains(untieredEmail, pendingVisitors);
        Assert.DoesNotContain(partnerEmail, pendingVisitors);

        var pendingOthers = await ListPendingEmailsAsync(
            token, "/api/v1/admin/others/pending/list");
        Assert.Contains(partnerEmail, pendingOthers);
        Assert.DoesNotContain(untieredEmail, pendingOthers);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<List<string>> ListEmailsAsync(string token, string url)
    {
        var response = await PostAuthAsync(url, new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminUserSummary>>>())!.Data!;
        return page.Items.Select(user => user.Email).ToList();
    }

    private async Task<List<string>> ListPendingEmailsAsync(string token, string url)
    {
        var response = await PostAuthAsync(url, new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!.Data!;
        return page.Items.Select(user => user.Email).ToList();
    }

    private async Task<string> CreateVisitorAsync(string token, Guid? profileTypeId)
    {
        var email = $"scope-visitor-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = "Scope Visitor",
                ProfileTypeId = profileTypeId,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    private async Task<string> CreateOtherAsync(string token, Guid profileTypeId)
    {
        var email = $"scope-other-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = "Scope Partner",
                ProfileTypeId = profileTypeId,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    private async Task<Guid> SeedProfileTypeAsync(bool isForVisitor)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Scope {Guid.NewGuid():N}",
            NameArabic = "نطاق",
            PageColor = "#244A77",
            IsForVisitor = isForVisitor,
            MobileAppRole = MobileAppRole.None,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(type);
        await appDb.SaveChangesAsync();
        return type.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"scope-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Scope Admin",
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
