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
/// D-115 — integration tests for the admin ProfileType CRUD surface
/// (<c>/admin/profile-types/*</c>). Confirms per-UserType name
/// uniqueness, cross-UserType same-name allowed, immutable UserType
/// post-create, soft-delete with in-use protection, and the 403 floor
/// for non-admin callers.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminProfileTypeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminProfileTypeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_can_create_get_list_and_soft_delete_a_visitor_profile_type()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"VVIP {Guid.NewGuid():N}";

        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                Name = name,
                NameArabic = "كبار الشخصيات",
                PageColor = "#FFD700",
                IsActive = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var summary = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(name, summary.Name);
        Assert.Equal("Visitor", summary.UserType);
        Assert.True(summary.IsActive);

        var get = await GetAuthAsync($"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery { Top = 100 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Id == summary.Id);

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        // Soft-delete is reflected in a subsequent get.
        var afterDelete = await GetAuthAsync($"/api/v1/admin/profile-types/{summary.Id}", adminToken);
        var afterDeleteBody = (await afterDelete.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(afterDeleteBody.IsActive);
    }

    [Fact]
    public async Task Admin_can_update_a_profile_type_without_touching_user_type()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Sponsor {Guid.NewGuid():N}";

        // D-186: partner-side profile types are UserType=Visitor with IsVisitor=false.
        var created = await CreateProfileTypeAsync(adminToken, "Visitor", name, "راعٍ", "#3B82F6");

        var renamed = $"{name} (Platinum)";
        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{created.Id}",
            new
            {
                Name = renamed,
                NameArabic = "راعٍ بلاتيني",
                PageColor = "#FFFFFF",
                IsActive = true,
                IsVisitor = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(renamed, updated.Name);
        // The route doesn't accept UserType in the body, so the value
        // cannot drift after create. D-186: every non-admin row is Visitor.
        Assert.Equal("Visitor", updated.UserType);
    }

    [Fact]
    public async Task Duplicate_name_within_the_same_user_type_returns_409()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Exhibitor {Guid.NewGuid():N}";

        // D-186: partner-side profile types live under UserType.Visitor.
        await CreateProfileTypeAsync(adminToken, "Visitor", name, "عارض", "#10B981");
        var second = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "عارض",
                PageColor = "#10B981",
                IsActive = true,
            },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeNameTaken, body.Error!.Code);
    }

    [Fact]
    public async Task Same_name_across_audience_and_partner_scope_returns_409()
    {
        // D-186: Visitor + Other used to be two distinct UserType scopes,
        // each with its own name-uniqueness bucket. After D-186 both are
        // UserType.Visitor; the audience-vs-partner split is IsVisitor.
        // The unique constraint is per (UserType, Name) so the same name
        // can no longer coexist across audience + partner. Documented as
        // expected behaviour — operators rename one of the rows.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Gold {Guid.NewGuid():N}";

        var audienceRow = await CreateProfileTypeAsync(
            adminToken, "Visitor", name, "ذهبي", "#FFD700");

        var partnerAttempt = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "ذهبي",
                PageColor = "#FFD700",
                IsActive = true,
            },
            adminToken);

        Assert.Equal(HttpStatusCode.Conflict, partnerAttempt.StatusCode);
        Assert.Equal("Visitor", audienceRow.UserType);
        Assert.True(audienceRow.IsVisitor);
    }

    [Fact]
    public async Task Create_for_Admin_user_type_returns_400()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Admin",
                Name = $"Should not land {Guid.NewGuid():N}",
                NameArabic = "لن يُسجَّل",
                PageColor = "#000000",
            },
            adminToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeInvalidUserType, body.Error!.Code);
    }

    [Fact]
    public async Task Cannot_delete_a_profile_type_that_is_still_referenced_by_a_user_profile()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        // Create a profile type and then attach a UserProfile to it.
        var pt = await CreateProfileTypeAsync(adminToken, "Visitor",
            $"InUse {Guid.NewGuid():N}", "قيد الاستخدام", "#3B82F6");

        Guid visitorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var v = new SimfUser
            {
                UserName = $"ptuser-{Guid.NewGuid():N}@simf.test",
                Email = $"ptuser-{Guid.NewGuid():N}@simf.test",
                EmailConfirmed = true,
                DisplayName = "Has Profile Type",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(v, AuthFlow.Password);
            visitorId = v.Id;

            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = visitorId,
                ProfileTypeId = pt.Id,
                CreatedAt = SimfClock.Now,
            });
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
        }

        var response = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{pt.Id}", adminToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ProfileTypeInUse, body.Error!.Code);
    }

    [Fact]
    public async Task A_non_admin_caller_is_forbidden_from_every_profile_type_endpoint()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery { Top = 10 },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                Name = "Should not land",
                NameArabic = "لن يُسجَّل",
                PageColor = "#000000",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var delete = await DeleteAuthAsync(
            $"/api/v1/admin/profile-types/{Guid.NewGuid()}", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{Guid.NewGuid()}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsVisitor_round_trips_through_Create_Get_List(bool isVisitor)
    {
        // D-186 review-pass (test-analyzer Gap 1): the audience-vs-partner
        // discriminator MUST persist through Create → Get → List. Without
        // this coverage a model-binding default or a missing EF mapping
        // would silently break every downstream scope guard.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"RoundTrip-{isVisitor}-{Guid.NewGuid():N}";

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = isVisitor,
                Name = name,
                NameArabic = "اختبار",
                PageColor = "#1F2937",
                IsActive = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(isVisitor, detail.IsVisitor);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}", adminToken);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.Equal(isVisitor, fetched.IsVisitor);

        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string>
                {
                    ["userType"] = "Visitor",
                    ["isVisitor"] = isVisitor ? "true" : "false",
                },
            }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        Assert.Contains(page.Items, row => row.Id == detail.Id);
        // Every returned row must match the requested IsVisitor filter.
        Assert.All(page.Items, row => Assert.Equal(isVisitor, row.IsVisitor));
    }

    [Fact]
    public async Task IsAppRegisterable_round_trips_through_Create_Get_and_Update()
    {
        // D-725 (owner item 1): the app-sign-up-picker visibility flag must
        // persist through Create → Get and be flippable on Update, so an admin
        // can hide a CP-only type (Staff / Moderator) or re-expose it.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"CpOnly {Guid.NewGuid():N}";

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "فريق",
                PageColor = "#10B981",
                IsActive = true,
                IsAppRegisterable = false,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(detail.IsAppRegisterable);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}", adminToken);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(fetched.IsAppRegisterable);

        // Flip it back on — the admin re-exposes the type in the app picker.
        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = detail.Name,
                NameArabic = detail.NameArabic,
                PageColor = detail.PageColor,
                IsActive = true,
                IsVisitor = detail.IsVisitor,
                IsAppRegisterable = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(after.IsAppRegisterable);

        // The grid projection must carry the flag too (the Edit modal pre-fills
        // from the row summary). After the flip IsAppRegisterable=true while
        // IsVisitor stays false — the two trailing bools now DIFFER, so a
        // swapped/mis-ordered projection would surface here.
        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string>
                {
                    ["userType"] = "Visitor",
                    ["isVisitor"] = "false",
                },
            }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        var listed = Assert.Single(page.Items, row => row.Id == detail.Id);
        Assert.True(listed.IsAppRegisterable);
        Assert.False(listed.IsVisitor);
    }

    [Fact]
    public async Task ShowInPartnerDirectory_can_be_turned_OFF_by_an_update()
    {
        // D-843 — the direction that was broken, and the reason the sibling test
        // below did not catch it. The route DTO omitted ShowInPartnerDirectory, so
        // FastEndpoints left it at the CONTRACT's default, which is `true`, and
        // AdminProfileTypeCommandService assigns it unconditionally.
        //
        // The drop therefore failed OPEN: unticking "show in partner directory"
        // returned a success toast and silently re-exposed the type in the Meet
        // People surfaces. false -> true passed (the drop forced the expected
        // answer); true -> false was untested and never worked.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Exposed {Guid.NewGuid():N}";

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "شريك",
                PageColor = "#10B981",
                IsActive = true,
                ShowInPartnerDirectory = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(detail.ShowInPartnerDirectory);

        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = detail.Name,
                NameArabic = detail.NameArabic,
                PageColor = detail.PageColor,
                IsActive = true,
                IsVisitor = detail.IsVisitor,
                ShowInPartnerDirectory = false,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(after.ShowInPartnerDirectory);

        // Re-read, so a response composed in memory cannot mask a stored `true`.
        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}", adminToken);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(fetched.ShowInPartnerDirectory);
    }

    [Fact]
    public async Task ShowInPartnerDirectory_round_trips_through_Create_Get_and_Update()
    {
        // D-760 (owner request): the "Meet People" visibility flag on a partner
        // type must persist through Create → Get and be flippable on Update, so an
        // admin can hide a whole Other type from the networking directory.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Hidden {Guid.NewGuid():N}";

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = false,
                Name = name,
                NameArabic = "شريك",
                PageColor = "#10B981",
                IsActive = true,
                ShowInPartnerDirectory = false,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(detail.ShowInPartnerDirectory);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}", adminToken);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(fetched.ShowInPartnerDirectory);

        // Flip it back on — the admin re-exposes the type in Meet People.
        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = detail.Name,
                NameArabic = detail.NameArabic,
                PageColor = detail.PageColor,
                IsActive = true,
                IsVisitor = detail.IsVisitor,
                ShowInPartnerDirectory = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(after.ShowInPartnerDirectory);

        // The grid projection must carry the new trailing flag too (the Edit
        // modal pre-fills from the row summary) — a mis-ordered projection of the
        // last positional bool would surface here.
        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string>
                {
                    ["userType"] = "Visitor",
                    ["isVisitor"] = "false",
                },
            }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        var listed = Assert.Single(page.Items, row => row.Id == detail.Id);
        Assert.True(listed.ShowInPartnerDirectory);
    }

    [Fact]
    public async Task IsVipTier_round_trips_through_Create_Get_Update_and_the_grid()
    {
        // The VIP-tier marker had NO admin write path — the seeder was the only
        // writer anywhere, so a profile type created from the Control Panel could
        // never be VIP however the admin filled the form, and no amount of CP work
        // could add a third VIP tier. It is now bound on create and update.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var name = $"Platinum {Guid.NewGuid():N}";

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                // Audience side: the VIP tier is only meaningful there.
                IsVisitor = true,
                Name = name,
                NameArabic = "بلاتيني",
                PageColor = "#E5E4E2",
                IsActive = true,
                IsVipTier = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(detail.IsVipTier);

        var get = await GetAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}", adminToken);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(fetched.IsVipTier);

        // Clearing it must actually clear it — the update assigns
        // unconditionally, so an admin can demote a tier.
        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{detail.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = detail.Name,
                NameArabic = detail.NameArabic,
                PageColor = detail.PageColor,
                IsActive = true,
                IsVisitor = detail.IsVisitor,
                IsVipTier = false,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(after.IsVipTier);

        // The grid projection carries it too. IsVipTier is the LAST positional
        // bool on the summary record, so a mis-ordered projection would land the
        // neighbouring flag here and surface as a wrong value, not a compile error.
        var list = await PostAuthAsync(
            "/api/v1/admin/profile-types/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string>
                {
                    ["userType"] = "Visitor",
                    ["isVisitor"] = "true",
                },
            }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>())!.Data!;
        var listed = Assert.Single(page.Items, row => row.Id == detail.Id);
        Assert.False(listed.IsVipTier);
        Assert.True(listed.IsVisitor);
    }

    [Fact]
    public async Task A_created_profile_type_is_not_VIP_unless_the_admin_asks()
    {
        // The default has to stay false: IsVipTier grants VIP-tier SEAT
        // self-reservation, so a type that silently defaulted to VIP would hand
        // every holder a protocol-adjacent seat.
        var adminToken = await CreateAdministratorAndSignInAsync();

        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = true,
                Name = $"Plain {Guid.NewGuid():N}",
                NameArabic = "عادي",
                PageColor = "#64748B",
                IsActive = true,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(detail.IsVipTier);
    }

    [Fact]
    public async Task Update_flipping_IsVisitor_persists_and_audits_the_change()
    {
        // D-186 review-pass (threat-detection H-1): an admin flipping
        // IsVisitor re-routes every linked account between approval
        // queues — the change MUST be audited with the old/new values
        // and the linked-account count.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var created = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = "Visitor",
                IsVisitor = true,
                Name = $"FlipMe {Guid.NewGuid():N}",
                NameArabic = "قلب",
                PageColor = "#10B981",
                IsActive = true,
            }, adminToken);
        var before = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.True(before.IsVisitor);

        var update = await PutAuthAsync(
            $"/api/v1/admin/profile-types/{before.Id}",
            new AdminUpdateProfileTypeRequest
            {
                Name = before.Name,
                NameArabic = before.NameArabic,
                PageColor = before.PageColor,
                IsActive = true,
                IsVisitor = false,
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var after = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
        Assert.False(after.IsVisitor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var auditRow = await db.OperationLog.AsNoTracking()
            .Where(e => e.EventType == "ProfileType.Updated"
                && e.Detail != null
                && e.Detail.Contains(before.Id.ToString()))
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(auditRow);
        Assert.NotNull(auditRow!.Detail);
        Assert.Contains("isVisitorChanged=true", auditRow.Detail!);
        Assert.Contains("isVisitorOld=True", auditRow.Detail);
        Assert.Contains("isVisitorNew=False", auditRow.Detail);
        Assert.Contains("linkedAccountCount=", auditRow.Detail);
    }

    [Fact]
    public async Task Create_others_rejects_an_audience_profile_type()
    {
        // D-186 review-pass (security H-1 + code-reviewer H-2):
        // POST /admin/others must reject a ProfileTypeId whose
        // ProfileType.IsVisitor=true. Without this guard the account
        // lands on the wrong queue with the wrong audit mapping.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var audience = await CreateProfileTypeAsync(
            adminToken, "Visitor",
            $"AudienceTier-{Guid.NewGuid():N}", "جمهور", "#FFD700");
        Assert.True(audience.IsVisitor);

        var response = await PostAuthAsync(
            "/api/v1/admin/others",
            new AdminCreateOtherRequest
            {
                Email = $"other-cross-{Guid.NewGuid():N}@simf.test",
                DisplayName = "Cross Other",
                ProfileTypeId = audience.Id,
            }, adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Pending_visitor_with_profile_row_but_null_ProfileTypeId_appears_on_Visitors_queue()
    {
        // D-186 review-pass (code-reviewer H-1): the self-signup flow
        // creates `new UserProfile { UserId, CreatedAt }` with
        // ProfileTypeId=null. The initial D-186 cut excluded such
        // users from the audience set (because withAnyProfileSet
        // contained their id but idsViaProfileType did not). The fix
        // redefines audience as `visitor MINUS partner-linked` so
        // null-ProfileTypeId users correctly land on the audience queue.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = Guid.NewGuid();
        var email = $"selfsignup-{subjectId:N}@simf.test";

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                Id = subjectId, UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Self Signup",
                AccountState = AccountState.PendingApproval,
                UserType = UserType.Visitor,
                CreatedAt = SimfClock.Now,
            };
            await users.CreateAsync(user, AuthFlow.Password);

            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = subjectId,
                ProfileTypeId = null,     // <-- the H-1 trigger
                CreatedAt = SimfClock.Now,
            });
            await appDb.SaveChangesAsync();
        }

        var list = await PostAuthAsync(
            "/api/v1/admin/visitors/pending/list",
            new GridQuery { Top = 200 }, adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminPendingUserSummary>>>())!.Data!;
        Assert.Contains(page.Items, row => row.Id == subjectId);
    }

    [Fact]
    public async Task Create_visitors_rejects_a_partner_profile_type()
    {
        // D-186 review-pass (twin of the test above for the audience
        // direction).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var partner = await CreateProfileTypeAsync(
            adminToken, "Visitor",
            $"PartnerTier-{Guid.NewGuid():N}", "شريك", "#10B981", isVisitor: false);
        Assert.False(partner.IsVisitor);

        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = $"visitor-cross-{Guid.NewGuid():N}@simf.test",
                DisplayName = "Cross Visitor",
                ProfileTypeId = partner.Id,
            }, adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<AdminProfileTypeSummary> CreateProfileTypeAsync(
        string adminToken, string userType, string name, string nameArabic, string pageColor,
        bool isVisitor = true)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/profile-types",
            new AdminCreateProfileTypeRequest
            {
                UserType = userType,
                IsVisitor = isVisitor,
                Name = name,
                NameArabic = nameArabic,
                PageColor = pageColor,
                IsActive = true,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminProfileTypeSummary>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"pt-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "ProfileType Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
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

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
