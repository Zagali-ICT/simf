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
/// D-127 integration tests for the on-site walk-in registration endpoints
/// (<c>POST /admin/{visitors,others}/register-onsite</c>). Confirms
/// auto-approve, QR minting in one transaction, optional-email behaviour
/// and the type-scoped profile-type guard.
/// </summary>
public sealed class WalkInRegistrationTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public WalkInRegistrationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Visitor_walk_in_creates_approved_user_with_qr_minted()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();

        var email = $"walkin-v-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email),
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.True(body.Success);
        Assert.NotEqual(Guid.Empty, body.Data!.UserId);
        Assert.False(string.IsNullOrEmpty(body.Data.QrId));
        Assert.Equal(email, body.Data.Email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == body.Data.UserId);
        Assert.Equal(AccountState.Approved, user.AccountState);
        Assert.Equal(UserType.Visitor, user.UserType);

        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(profileTypeId, profile.ProfileTypeId);
        Assert.False(string.IsNullOrEmpty(profile.QrId));
        Assert.Equal("Walk-in Visitor", profile.EnglishName);
    }

    [Fact]
    public async Task Other_walk_in_creates_approved_other_user()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetOtherProfileTypeAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/others/register-onsite",
            BuildRequest(profileTypeId, $"walkin-o-{Guid.NewGuid():N}@simf.test"),
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == body.Data!.UserId);
        // D-186: Other walk-ins are now Visitor-typed accounts; the
        // partner status lives on the linked ProfileType.IsVisitor=false.
        Assert.Equal(UserType.Visitor, user.UserType);
        Assert.Equal(AccountState.Approved, user.AccountState);
    }

    [Fact]
    public async Task Walk_in_without_email_synthesizes_placeholder()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();

        var req = BuildRequest(profileTypeId, email: null);
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.StartsWith("walkin-", body.Data!.Email);
        Assert.EndsWith("@simf.local", body.Data.Email);
    }

    [Fact]
    public async Task Walk_in_routing_now_trusts_chosen_profile_type_after_D186()
    {
        // D-186: the walk-in endpoint pair (/visitors/register-onsite +
        // /others/register-onsite) used to enforce a strict cross-scope
        // 400. After the UserType collapse both desks produce Visitor-
        // typed accounts; the audience/partner queue assignment derives
        // from the chosen ProfileType.IsVisitor on the request. A desk
        // submitting a partner-side ProfileType via /visitors/register-
        // onsite simply creates a Visitor user whose linked partner
        // ProfileType routes them to the Others approval queue.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var partnerProfileTypeId = await GetOtherProfileTypeAsync();

        var req = BuildRequest(partnerProfileTypeId, $"crossed-{Guid.NewGuid():N}@simf.test");
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Walk_in_with_duplicate_email_returns_409()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var email = $"walkin-dup-{Guid.NewGuid():N}@simf.test";

        var first = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email), adminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email), adminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_non_admin_caller_is_forbidden()
    {
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, $"x-{Guid.NewGuid():N}@simf.test"),
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static AdminWalkInRegistrationRequest BuildRequest(Guid profileTypeId, string? email) =>
        new()
        {
            Email = email,
            DisplayName = "Walk-in Subject",
            ArabicName = "زائر فوري",
            EnglishName = "Walk-in Visitor",
            ProfileTypeId = profileTypeId,
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = "1234567890",
            SaudiMobile = "+966500000001",
        };

    private async Task<Guid> GetVisitorProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.UserType == UserType.Visitor && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new ProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — WalkInTestSeed",
            NameArabic = "زائر — اختبار",
            PageColor = "#3B82F6",
            UserType = UserType.Visitor,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    private async Task<Guid> GetOtherProfileTypeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // D-186: partner-side ProfileTypes are UserType.Visitor with
        // IsVisitor=false.
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.UserType == UserType.Visitor
                                       && p.IsVisitor == false
                                       && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new ProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — WalkInTestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            UserType = UserType.Visitor,
            IsVisitor = false,
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
        var email = $"walkin-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "WalkIn Test Admin",
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
                Email = email, Password = Password, Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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
