// BUG-018 — the gate OPERATOR model. Owner ruling: gate scanning happens through
// the mobile app, so a gate operator is an operational non-admin app account (a
// partner ProfileType with IsForVisitor=false carrying Staff/Moderator), not a
// Control-Panel admin. These tests pin:
//   18-1  the candidate list offers those accounts (and never an admin account);
//   18-2  the API rejects an ineligible operator id and names it;
//   18-3  an operational profile type is exempt from the VISITOR completeness
//         rules (interests / ID document / male face photo) that used to divert a
//         seeded gate operator to the visitor "Create profile" form forever;
//   18-4  both gate-form lookups are reachable with only Gates.Manage;
//   18-5  the new FluentValidation validator rejects a malformed gate request;
//   18-6  the assignments row carries the operator's email for the detail view.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Gates)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GateOperatorModelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string ProfilePath = "/api/v1/app/account/user-profile";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateOperatorModelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // -- 18-1 / 18-7 — the candidate picker ----------------------------------

    [Fact]
    public async Task Operator_candidates_offer_an_approved_staff_profile_account()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved);

        var page = await ListCandidatesAsync(adminToken, staff.Email);

        var row = Assert.Single(page.Items);
        Assert.Equal(staff.UserId, row.UserId);
        Assert.Equal(staff.Email, row.Email);
        Assert.Equal(MobileAppRole.Staff, row.MobileAppRole);
        Assert.False(string.IsNullOrWhiteSpace(row.ProfileTypeName));
    }

    [Fact]
    public async Task Operator_candidates_never_offer_a_control_panel_admin_account()
    {
        // The ROOT CAUSE: the CP picker was bound to the admin-accounts list, so
        // every offered operator resolved to MobileAppRole.None and could never
        // scan from the app.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var adminEmail = await CreateAdminAccountAsync();

        var page = await ListCandidatesAsync(adminToken, adminEmail);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Operator_candidates_exclude_unapproved_and_non_operational_accounts()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var pendingStaff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.PendingApproval);
        var disabledStaff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Disabled);
        var exhibitor = await SeedPartnerAccountAsync(
            MobileAppRole.Exhibitor, AccountState.Approved);

        Assert.Empty((await ListCandidatesAsync(adminToken, pendingStaff.Email)).Items);
        Assert.Empty((await ListCandidatesAsync(adminToken, disabledStaff.Email)).Items);
        Assert.Empty((await ListCandidatesAsync(adminToken, exhibitor.Email)).Items);
    }

    // -- 18-2 — the backend enforces the rule --------------------------------

    [Fact]
    public async Task Assigning_a_plain_visitor_as_an_operator_is_a_400_naming_the_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var visitorUserId = await SeedPlainVisitorAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            NewGateRequest(operators: [visitorUserId]),
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateAssignmentInvalid, body.Error!.Code);
        Assert.Contains(visitorUserId.ToString(), body.Error.Message);
    }

    [Fact]
    public async Task Assigning_an_unapproved_staff_account_as_an_operator_is_a_400()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var pending = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.PendingApproval);

        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            NewGateRequest(operators: [pending.UserId]),
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateAssignmentInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Assigning_an_approved_staff_account_as_an_operator_succeeds()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved);

        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            NewGateRequest(operators: [staff.UserId]),
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
        Assert.Contains(staff.UserId, detail.AssignedOperatorUserIds);
    }

    // -- 18-6 — the detail view can name the operators ------------------------

    [Fact]
    public async Task Gate_assignments_carry_the_operator_name_and_email()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved);

        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            NewGateRequest(operators: [staff.UserId]),
            adminToken);
        var gate = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;

        var assignments = await GetAuthAsync(
            $"/api/v1/admin/gates/{gate.Id}/assignments", adminToken);
        Assert.Equal(HttpStatusCode.OK, assignments.StatusCode);
        var rows = (await assignments.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminGateAssignmentRow>>>())!.Data!;

        var row = Assert.Single(rows);
        Assert.Equal(staff.UserId, row.UserId);
        Assert.Equal(staff.Email, row.UserEmail);
        Assert.False(string.IsNullOrWhiteSpace(row.UserDisplayName));
    }

    // -- 18-4 — the gate form's lookups only need Gates.Manage ----------------

    [Fact]
    public async Task Gate_form_lookups_are_reachable_with_only_Gates_Manage()
    {
        // A Security-team style gate manager: Gates.Manage and nothing else. Both
        // lookups used to sit behind Admins.View / ProfileTypes.View / Halls.View,
        // so this caller saw three silently empty dropdowns.
        var token = await CreateAdminWithCustomRoleAsync([PermissionCatalog.Gates.Manage]);

        var options = await GetAuthAsync("/api/v1/admin/gates/form-options", token);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);

        var candidates = await PostAuthAsync(
            "/api/v1/admin/gates/operator-candidates/list", new GridQuery { Top = 25 }, token);
        Assert.Equal(HttpStatusCode.OK, candidates.StatusCode);
    }

    [Fact]
    public async Task Gate_form_lookups_are_forbidden_without_Gates_Manage()
    {
        var token = await CreateAdminWithCustomRoleAsync([PermissionCatalog.Sessions.View]);

        var options = await GetAuthAsync("/api/v1/admin/gates/form-options", token);
        var candidates = await PostAuthAsync(
            "/api/v1/admin/gates/operator-candidates/list", new GridQuery { Top = 25 }, token);

        Assert.Equal(HttpStatusCode.Forbidden, options.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, candidates.StatusCode);
    }

    // -- 18-5 — the new gate request validator -------------------------------

    [Fact]
    public async Task Create_gate_with_a_blank_english_name_is_rejected()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var request = NewGateRequest();
        request.Name = string.Empty;

        var response = await PostAuthAsync("/api/v1/admin/gates", request, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_gate_with_an_over_long_code_is_rejected()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var request = NewGateRequest();
        request.Code = new string('A', 17);   // EF HasMaxLength(16)

        var response = await PostAuthAsync("/api/v1/admin/gates", request, adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- 18-3 — an operational account is not held to the visitor rules -------

    [Fact]
    public async Task Staff_profile_account_reads_complete_without_interests_id_or_photo()
    {
        // BLOCKER A: the seeded Demo Staff account landed on the visitor "Create
        // profile" form on every sign-in because profileComplete stayed false.
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved, gender: Gender.Male);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();

        Assert.True(await service.IsProfileCompleteAsync(staff.UserId));
    }

    [Fact]
    public async Task Staff_profile_account_without_names_still_reads_incomplete()
    {
        // The exemption is scoped: names stay required for everyone.
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved, gender: Gender.Male,
            withNames: false);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();

        Assert.False(await service.IsProfileCompleteAsync(staff.UserId));
    }

    [Fact]
    public async Task Visitor_profile_account_still_needs_interests_id_and_face_photo()
    {
        // The audience side is UNCHANGED — the same seed on a visitor-side profile
        // type reads incomplete.
        var visitor = await SeedPartnerAccountAsync(
            MobileAppRole.None, AccountState.Approved, gender: Gender.Male,
            isForVisitor: true);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();

        Assert.False(await service.IsProfileCompleteAsync(visitor.UserId));
    }

    [Fact]
    public async Task Male_staff_profile_account_can_save_a_profile_without_a_face_photo()
    {
        // The second half of BLOCKER A: the male-face hard reject made the form the
        // operator was diverted to unsubmittable, so the account could never leave it.
        var staff = await SeedPartnerAccountAsync(
            MobileAppRole.Staff, AccountState.Approved, gender: Gender.Male);
        var token = await SignInAppAsync(staff.Email);

        var request = await ValidSaudiRequestAsync();
        request.Gender = Gender.Male;

        var upsert = await PostAuthAsync(ProfilePath, request, token);

        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static AdminCreateGateRequest NewGateRequest(List<Guid>? operators = null) =>
        new()
        {
            Code = $"GO-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Name = "Operator Model Gate",
            NameArabic = "بوابة نموذج المشغّل",
            DirectionMode = DirectionMode.Both,
            AssignedOperatorUserIds = operators ?? new List<Guid>(),
        };

    private async Task<GridPage<AdminGateOperatorCandidate>> ListCandidatesAsync(
        string adminToken, string search)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/gates/operator-candidates/list",
            new GridQuery { Top = 50, Search = search },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminGateOperatorCandidate>>>())!.Data!;
    }

    /// <summary>Seeds an app account on a partner (or, when
    /// <paramref name="isForVisitor"/>, an audience) profile type carrying
    /// <paramref name="role"/>, with names but deliberately NO interests, NO ID
    /// document and NO avatar — the exact shape an admin-created gate operator has.</summary>
    private async Task<(Guid UserId, string Email)> SeedPartnerAccountAsync(
        MobileAppRole role, AccountState state,
        Gender gender = Gender.Unspecified,
        bool isForVisitor = false,
        bool withNames = true)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Gate Staff {Guid.NewGuid():N}",
            NameArabic = "موظف بوابة",
            PageColor = "#244A77",
            IsForVisitor = isForVisitor,
            MobileAppRole = role,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(profileType);
        await appDb.SaveChangesAsync();

        var email = $"gate-op-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Gate Operator",
            AccountState = state,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileType.Id,
            Name = withNames ? "Gate Operator Account" : string.Empty,
            NameArabic = withNames ? "حساب مشغّل بوابة" : string.Empty,
            Gender = gender,
            PlaceOfBirth = "Riyadh",
            NationalityId = 0,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();

        return (user.Id, email);
    }

    /// <summary>An approved audience account with no profile type at all — the
    /// plain visitor an admin must not be able to assign as a gate operator.</summary>
    private async Task<Guid> SeedPlainVisitorAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var email = $"gate-visitor-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Plain Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdminAccountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var email = $"gate-cpadmin-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Control Panel Admin",
            AccountState = AccountState.Approved,
            UserType = UserType.Admin,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return email;
    }

    private async Task<UpsertUserProfileRequest> ValidSaudiRequestAsync()
    {
        Guid organisationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var organisation = new SIMF.Domain.Organisations.Organisation
            {
                Id = Guid.NewGuid(),
                Name = $"Gate Ops Organisation {Guid.NewGuid():N}",
                NameArabic = $"جهة اختبار {Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            appDb.Organisations.Add(organisation);
            await appDb.SaveChangesAsync();
            organisationId = organisation.Id;
        }

        return new UpsertUserProfileRequest
        {
            // Required since 2026-08-29: every field on the app form is
            // mandatory except the plate number. Female because the face-photo
            // rule applies to males only, and these fixtures carry no avatar -
            // a test that wants the male path sets Gender itself.
            JobTitle = "Engineer",
            JobTitleArabic = "مهندس",
            Gender = Gender.Female,
            InterestIds = new List<Guid>(),
            ArabicName = "محمد عبدالله أحمد الزهراني",
            EnglishName = "Gate Operator Account",
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = TestIdentity.MintNationalId(),
            OrganisationId = organisationId,
            // DEF-PHN-004 — the mobile is required on the upsert now.
            SaudiMobile = "0501234567",
        };
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"gate-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Gate Model Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await SignInCpAsync(email);
    }

    // Mirrors PermissionEnforcementTests.CreateAdminWithCustomRoleAsync — the
    // seeder does not run under the Testing host, so the Permission rows for the
    // granted codes are inserted here.
    private async Task<string> CreateAdminWithCustomRoleAsync(string[] grantedCodes)
    {
        var email = $"gate-limited-{Guid.NewGuid():N}@simf.test";
        var roleName = $"GateLimited-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

            var role = new SimfRole { Name = roleName, IsBaseline = false };
            await roleManager.CreateAsync(role);

            foreach (var code in grantedCodes)
            {
                var def = PermissionCatalog.All.Single(permission => permission.Code == code);
                var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Code == code);
                if (permission is null)
                {
                    permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Code = def.Code,
                    };
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
                DisplayName = "Gate Limited Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }

        return await SignInCpAsync(email);
    }

    private async Task<string> SignInCpAsync(string email) =>
        await AuthFlow.SignInControlPanelAsync(_client, _factory, email);

    private async Task<string> SignInAppAsync(string email) =>
        await SignInAsync(email, SignInAudience.App);

    private async Task<string> SignInAsync(string email, SignInAudience audience)
    {
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = audience,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
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

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
