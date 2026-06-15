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

/// <summary>
/// V-1 (D-429) integration tests for the VVIP/VIP welcome roster + export
/// (<c>GET /admin/visitors/vip/roster</c>, <c>.../roster/list</c>,
/// <c>.../roster/export</c>). Confirms the roster includes VVIP/VIP visitors
/// with their موج extras, excludes non-VIP tiers, and renders a CSV.
/// </summary>
public sealed class VipRosterTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public VipRosterTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Roster_includes_vvip_with_mawj_data_and_excludes_non_vip()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var vvipTypeId = await GetVisitorProfileTypeByNameAsync("VVIP");
        var normalTypeId = await GetVisitorProfileTypeByNameAsync("Normal");
        var organisationId = await GetOrganisationIdAsync();

        var mawjId = $"MAWJ-{Guid.NewGuid():N}"[..16];
        var vvip = BuildRequest(vvipTypeId, $"vvip-{Guid.NewGuid():N}@simf.test", organisationId);
        vvip.MawjId = mawjId;
        vvip.Honorific = "Minister";
        vvip.PreferredLanguage = "ar";
        var vvipReg = await PostAuthAsync("/api/v1/admin/visitors/register-onsite", vvip, adminToken);
        Assert.Equal(HttpStatusCode.OK, vvipReg.StatusCode);

        // A Normal-tier walk-in must NOT appear in the VIP roster.
        var normalReg = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(normalTypeId, $"normal-{Guid.NewGuid():N}@simf.test", organisationId),
            adminToken);
        Assert.Equal(HttpStatusCode.OK, normalReg.StatusCode);
        var normalId = (await normalReg.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!.Data!.UserId;

        var rosterResponse = await GetAuthAsync("/api/v1/admin/visitors/vip/roster", adminToken);
        Assert.Equal(HttpStatusCode.OK, rosterResponse.StatusCode);
        var roster = (await rosterResponse.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<VipRosterRow>>>())!;
        Assert.True(roster.Success);

        var row = Assert.Single(roster.Data!, r => r.MawjId == mawjId);
        Assert.Equal("VVIP", row.TierName);
        Assert.Equal("Minister", row.Honorific);
        Assert.Equal("ar", row.PreferredLanguage);
        Assert.DoesNotContain(roster.Data!, r => r.UserId == normalId);
    }

    [Fact]
    public async Task Roster_csv_export_contains_header_and_mawj_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var vipTypeId = await GetVisitorProfileTypeByNameAsync("VIP");
        var organisationId = await GetOrganisationIdAsync();

        var mawjId = $"MAWJX{Guid.NewGuid():N}"[..14];
        var vip = BuildRequest(vipTypeId, $"vip-{Guid.NewGuid():N}@simf.test", organisationId);
        vip.MawjId = mawjId;
        await PostAuthAsync("/api/v1/admin/visitors/register-onsite", vip, adminToken);

        var response = await GetAuthAsync(
            "/api/v1/admin/visitors/vip/roster/export?format=csv", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("Mawj ID", csv);
        Assert.Contains(mawjId, csv);
    }

    [Fact]
    public async Task Roster_requires_export_permission()
    {
        // A visitor token lacks Visitors.ExportVip → 403.
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await GetAuthAsync("/api/v1/admin/visitors/vip/roster", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static AdminWalkInRegistrationRequest BuildRequest(
        Guid profileTypeId, string email, Guid organisationId) =>
        new()
        {
            Email = email,
            DisplayName = "VIP Subject",
            ArabicName = "ضيف مهم",
            EnglishName = "VIP Guest",
            ProfileTypeId = profileTypeId,
            NationalityCode = "SA",
            IsSaudi = true,
            NationalId = "1234567890",
            SaudiMobile = "+966500000001",
            OrganisationId = organisationId,
        };

    private async Task<Guid> GetOrganisationIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.Organisations.FirstOrDefaultAsync(o => o.IsActive);
        if (existing is not null) return existing.Id;
        var fresh = new SIMF.Domain.Organisations.Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = "جهة اختبار",
            Name = "Test Organisation",
            CommercialRegistration = $"CR{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Organisations.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    /// <summary>Find-or-create an audience-side profile type with the given name
    /// (VVIP / VIP / Normal) so the test is independent of seed order.</summary>
    private async Task<Guid> GetVisitorProfileTypeByNameAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.ProfileTypes.FirstOrDefaultAsync(p => p.Name == name);
        if (existing is not null) return existing.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = name,
            PageColor = "#3B82F6",
            IsForVisitor = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"vip-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "VIP Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
