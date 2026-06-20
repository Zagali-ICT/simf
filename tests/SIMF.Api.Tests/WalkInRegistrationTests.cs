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
/// D-127 (amended D-425) integration tests for the on-site walk-in registration
/// endpoints (<c>POST /admin/{visitors,others}/register-onsite</c>). Confirms
/// the D-425 create-as-PendingApproval behaviour (no QR until approval),
/// optional-email behaviour and the type-scoped profile-type guard.
/// </summary>
public sealed class WalkInRegistrationTests : IClassFixture<SimfApiFactory>
{
    private const string Password = "Zx9#mKp2!";

    // A minimal valid 1x1 PNG (magic-byte + IHDR + IDAT + IEND) for the
    // D-427 admin avatar-upload test.
    private static readonly byte[] OnePixelPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public WalkInRegistrationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Visitor_walk_in_creates_pending_user_without_qr()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var email = $"walkin-v-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email, organisationId),
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.True(body.Success);
        Assert.NotEqual(Guid.Empty, body.Data!.UserId);
        // D-425 — a pending walk-in carries no QR until an admin approves it.
        Assert.True(string.IsNullOrEmpty(body.Data.QrId));
        Assert.Equal(email, body.Data.Email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == body.Data.UserId);
        Assert.Equal(AccountState.PendingApproval, user.AccountState);
        Assert.Equal(UserType.Visitor, user.UserType);

        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(profileTypeId, profile.ProfileTypeId);
        Assert.True(string.IsNullOrEmpty(profile.QrId));
        Assert.Equal("Walk-in Visitor", profile.Name);
    }

    [Fact]
    public async Task Walk_in_visitor_appears_in_pending_queue_then_approve_mints_qr()
    {
        // Task #6 — the full walk-in chain end-to-end through the real routes:
        // register-onsite (PendingApproval, no QR) -> the account shows up in the
        // pending-visitors queue -> approve -> Approved + QR minted -> it leaves
        // the queue. This is the regression the CS-A..E reviewer asked for.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        // 1) Walk-in register -> PendingApproval, no QR at the desk (D-425).
        var email = $"walkin-e2e-{Guid.NewGuid():N}@simf.test";
        var reg = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email, organisationId), adminToken);
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        var regBody = (await reg.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        var subjectId = regBody.Data!.UserId;
        Assert.True(string.IsNullOrEmpty(regBody.Data.QrId));

        // 2) The registrant appears in the pending-visitors queue.
        var listResp = await PostAuthAsync(
            "/api/v1/admin/visitors/pending/list",
            new GridQuery { Top = 200 }, adminToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var page = (await listResp.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!;
        Assert.Contains(page.Data!.Items, row => row.Id == subjectId);

        // 3) Approve -> Approved + the QR is minted on the profile (D-386).
        var approve = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.Approved, user.AccountState);
        var qrId = await appDb.UserProfiles
            .Where(p => p.UserId == subjectId)
            .Select(p => p.QrId)
            .SingleAsync();
        Assert.False(string.IsNullOrEmpty(qrId));

        // 4) The approved visitor no longer shows in the pending queue.
        var listAfter = await PostAuthAsync(
            "/api/v1/admin/visitors/pending/list",
            new GridQuery { Top = 200 }, adminToken);
        var pageAfter = (await listAfter.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!;
        Assert.DoesNotContain(pageAfter.Data!.Items, row => row.Id == subjectId);
    }

    [Fact]
    public async Task Admin_uploads_visitor_avatar_sets_path()
    {
        // D-427 (CS-3) — the desk captures a profile photo (avatar) for the
        // walk-in account via the new admin avatar endpoint.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var reg = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, $"walkin-av-{Guid.NewGuid():N}@simf.test", organisationId),
            adminToken);
        var regBody = (await reg.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        var subjectId = regBody.Data!.UserId;

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(OnePixelPng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "avatar.png");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/visitors/{subjectId}/avatar") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.False(string.IsNullOrEmpty(user.AvatarRelativePath));
    }

    [Fact]
    public async Task Visitor_walk_in_persists_vip_fields()
    {
        // V-1 (D-429) — the VIP page sends the موج extras (Mawj ID, honorific,
        // preferred language); they must land on the profile row.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(profileTypeId, $"walkin-vipf-{Guid.NewGuid():N}@simf.test", organisationId);
        req.MawjId = "MAWJ-12345";
        req.Honorific = "Minister";
        req.PreferredLanguage = "ar";

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == body.Data!.UserId);
        Assert.Equal("MAWJ-12345", profile.MawjId);
        Assert.Equal("Minister", profile.Honorific);
        Assert.Equal("ar", profile.PreferredLanguage);
    }

    [Fact]
    public async Task Admin_uploads_vip_photo_sets_path()
    {
        // V-1 (D-429) — the VIP page captures a separate welcome photo via the
        // dedicated vip-photo endpoint; it must set VipPhotoRelativePath (a
        // field distinct from the avatar).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var reg = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, $"walkin-vp-{Guid.NewGuid():N}@simf.test", organisationId),
            adminToken);
        var regBody = (await reg.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        var subjectId = regBody.Data!.UserId;

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(OnePixelPng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "vip.png");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/admin/visitors/{subjectId}/vip-photo") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == subjectId);
        Assert.False(string.IsNullOrEmpty(profile.VipPhotoRelativePath));
    }

    [Fact]
    public async Task Other_walk_in_creates_pending_other_user()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetOtherProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/others/register-onsite",
            BuildRequest(profileTypeId, $"walkin-o-{Guid.NewGuid():N}@simf.test", organisationId),
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
        Assert.Equal(AccountState.PendingApproval, user.AccountState);
    }

    [Fact]
    public async Task Walk_in_without_email_synthesizes_placeholder()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(profileTypeId, email: null, organisationId);
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;
        Assert.StartsWith("walkin-", body.Data!.Email);
        Assert.EndsWith("@simf.local", body.Data.Email);
    }

    [Fact]
    public async Task Walk_in_with_partner_profile_type_on_visitors_desk_returns_400()
    {
        // D-186 + review-pass: the initial D-186 cut dropped the
        // cross-scope guard on walk-in. The review-pass restored it
        // (security H-3 + code-reviewer H-2/H-3): the Visitors desk
        // passes expectedIsVisitor=true, the Others desk passes false,
        // and a desk that picks the wrong-scope ProfileType is
        // rejected with AdminProfileTypeInvalid rather than silently
        // routing the account to the wrong queue with the wrong
        // audit-event mapping.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var partnerProfileTypeId = await GetOtherProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(partnerProfileTypeId, $"crossed-{Guid.NewGuid():N}@simf.test", organisationId);
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Walk_in_with_audience_profile_type_on_others_desk_returns_400()
    {
        // D-186 review-pass: mirror of the above for the partner desk.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var audienceProfileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(audienceProfileTypeId, $"crossed-{Guid.NewGuid():N}@simf.test", organisationId);
        var response = await PostAuthAsync(
            "/api/v1/admin/others/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Walk_in_with_duplicate_email_returns_409()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();
        var email = $"walkin-dup-{Guid.NewGuid():N}@simf.test";

        var first = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email, organisationId), adminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite",
            BuildRequest(profileTypeId, email, organisationId), adminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Walk_in_without_organisation_returns_400()
    {
        // B3 — D-221: organisation is required; a null id fails validation.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();

        var req = BuildRequest(profileTypeId, $"noorg-{Guid.NewGuid():N}@simf.test", organisationId: null);
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Walk_in_with_unknown_organisation_returns_400()
    {
        // B3 — D-221: a non-null id that resolves to no active organisation is
        // rejected by the service with OrganisationInvalid (clean 400, no FK error).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();

        var req = BuildRequest(profileTypeId, $"badorg-{Guid.NewGuid():N}@simf.test", Guid.NewGuid());
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OrganisationInvalid, body.Error!.Code);
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

    [Fact]
    public async Task Walk_in_with_a_non_luhn_national_id_returns_400()
    {
        // D-459 (review) — the desk enforces the Luhn checksum. Negative case:
        // the happy path uses a Luhn-valid id, so without this a broken Luhn rule
        // would go unnoticed.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(profileTypeId, $"walkin-luhn-{Guid.NewGuid():N}@simf.test", organisationId);
        req.NationalId = "1234567890"; // valid prefix + length, fails Luhn

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Walk_in_arabic_plate_is_stored_as_the_canonical_latin_code()
    {
        // D-468 (review) — the desk stores the canonical Latin plate code, like
        // the self-service path, so an Arabic-script entry persists as Latin.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var profileTypeId = await GetVisitorProfileTypeAsync();
        var organisationId = await GetOrganisationIdAsync();

        var req = BuildRequest(profileTypeId, $"walkin-plate-{Guid.NewGuid():N}@simf.test", organisationId);
        req.PlateNumber = "ابح1234"; // Arabic A/B/J + digits

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors/register-onsite", req, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminWalkInRegistrationResponse>>())!;

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = await appDb.UserProfiles.SingleAsync(p => p.UserId == body.Data!.UserId);
        Assert.Equal("ABJ1234", profile.PlateNumber);
    }

    // -- Helpers --------------------------------------------------------------

    private static AdminWalkInRegistrationRequest BuildRequest(
        Guid profileTypeId, string? email, Guid? organisationId = null) =>
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
            NationalId = "1101798278",   // D-459 — Luhn-valid Saudi national id
            SaudiMobile = "+966500000001",
            // B3 — D-221: organisation is required at the desk.
            OrganisationId = organisationId,
        };

    // B3 — D-221: ensure an active organisation exists and return its id so the
    // success-path requests satisfy the new required-organisation rule.
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

    private async Task<Guid> GetVisitorProfileTypeAsync()
    {
        // D-186 review-pass: the Visitors desk endpoint now enforces
        // expectedIsVisitor=true, so this helper MUST return an
        // audience-side ProfileType (IsVisitor=true).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var seeded = await appDb.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor == true
                                       && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Visitor — WalkInTestSeed",
            NameArabic = "زائر — اختبار",
            PageColor = "#3B82F6",
            IsForVisitor = true,
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
            .FirstOrDefaultAsync(p => p.IsForVisitor == false
                                       && p.IsActive);
        if (seeded is not null) return seeded.Id;
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = "Other — WalkInTestSeed",
            NameArabic = "أخرى — اختبار",
            PageColor = "#10B981",
            IsForVisitor = false,
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
            "/api/v1/app/auth/sign-in",
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
