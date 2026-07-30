// VIP edit (2026-07-21) — bUnit tests for the VIP update capability:
//   • EditAccountForm gained a "Photo & ID" section (avatar + ID document, plus
//     the VVIP/VIP welcome photo only when ShowVipPhoto is set).
//   • VipRegistration (/admin/visitors/vip) is the VIP/VVIP list page with a
//     permission-gated "New VIP" (registration wizard) + per-row Edit, wired
//     conditionally so a Visitors.RegisterOnsite/Edit-less admin sees neither.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class VipEditTests : CpComponentTestBase
{
    private static AdminProfileTypeSummary VisitorTier(string name) =>
        new(Guid.NewGuid(), name, name, "#244A77", "Visitor", "None",
            IsActive: true, IsVisitor: true, IsAppRegisterable: true);

    private static AdminUserProfileView Profile(
        Guid id, bool hasAvatar, bool hasIdImage, Guid? tierId) =>
        new(id, "vip@simf.test", "HRH Faisal", "Visitor", "Approved",
            tierId, "VIP", "كبار", "#244A77", "QR-1", "فيصل", "Faisal",
            JobTitle: null, NationalityCode: "SA", DateOfBirth: null,
            PlaceOfBirth: null, IsSaudi: true, NationalId: null,
            IqamaNumber: null, PassportNumber: null, SaudiMobile: null,
            InternationalMobile: null, HasIdImage: hasIdImage, HasAvatar: hasAvatar,
            InterestIds: Array.Empty<Guid>(), RejectionReason: null,
            RejectionReasonArabic: null, CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);

    private static AdminVipSummary VipRow(Guid userId) =>
        new(UserProfileId: Guid.NewGuid(), UserId: userId, EnglishName: "HRH Faisal",
            ArabicName: "فيصل", JobTitle: null, JobTitleArabic: null,
            ProfileTypeName: "VIP", ProfileTypeNameArabic: "كبار", Email: "vip@simf.test");

    private void StubEditAccountFormLoads(Guid accountId, bool hasAvatar, bool hasIdImage)
    {
        var tier = VisitorTier("VIP");
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson",
                inv => inv.Arguments.Count > 0
                    && inv.Arguments[0] is string url && url.Contains("profile-types"))
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary> { tier }));
        JSInterop.Setup<ApiResult<AdminUserProfileView>>(
                "simfAccount.getJson",
                inv => inv.Arguments.Count > 0
                    && inv.Arguments[0] is string url && url.EndsWith("/profile"))
            .SetResult(ApiResult<AdminUserProfileView>.Ok(
                Profile(accountId, hasAvatar, hasIdImage, tier.Id)));
        // B22 — the form also reads the active-country list for its nationality picker.
        JSInterop.Setup<ApiResult<CountryListResponse>>(
                "simfAccount.getJson",
                inv => inv.Arguments.Count > 0
                    && inv.Arguments[0] is string url && url.EndsWith("walk-in/countries"))
            .SetResult(ApiResult<CountryListResponse>.Ok(
                new CountryListResponse(new List<CountryDto>
                {
                    new("SA", "Saudi Arabia", "السعودية"),
                    new("EG", "Egypt", "مصر"),
                })));
    }

    private void StubVipList()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminVipSummary>>>("simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminVipSummary>>.Ok(new GridPage<AdminVipSummary>
            {
                Items = new[] { VipRow(Guid.NewGuid()) },
                Total = 1,
            }));
    }

    [Fact]
    public void EditAccountForm_renders_photo_and_id_section_without_vip_photo()
    {
        var accountId = Guid.NewGuid();
        StubEditAccountFormLoads(accountId, hasAvatar: true, hasIdImage: true);

        var cut = RenderComponent<EditAccountForm>(p => p
            .Add(c => c.AccountId, accountId)
            .Add(c => c.Scope, "visitors")
            .Add(c => c.IsVisitorScope, true)
            .Add(c => c.ShowVipPhoto, false));

        Assert.Contains("Admin.Edit.Photo.Section", cut.Markup);
        Assert.Contains("Admin.Edit.Photo.Avatar", cut.Markup);
        Assert.Contains("Admin.Edit.Photo.IdDocument", cut.Markup);
        Assert.Contains("/avatar?v=", cut.Markup);
        Assert.Contains("/id-document?v=", cut.Markup);
        Assert.DoesNotContain("Admin.Edit.Photo.VipPhoto", cut.Markup);
        Assert.Contains("edit-account-avatar", cut.Markup);
    }

    [Fact]
    public void EditAccountForm_shows_vip_photo_when_enabled()
    {
        var accountId = Guid.NewGuid();
        StubEditAccountFormLoads(accountId, hasAvatar: false, hasIdImage: false);

        var cut = RenderComponent<EditAccountForm>(p => p
            .Add(c => c.AccountId, accountId)
            .Add(c => c.Scope, "visitors")
            .Add(c => c.IsVisitorScope, true)
            .Add(c => c.ShowVipPhoto, true));

        Assert.Contains("Admin.Edit.Photo.VipPhoto", cut.Markup);
        Assert.Contains("edit-account-vip-photo", cut.Markup);
        Assert.DoesNotContain("/avatar?v=", cut.Markup);
        Assert.DoesNotContain("/id-document?v=", cut.Markup);
    }

    // B22 — the Edit form carries a Nationality picker (the only admin path that can
    // correct a nationality, which gates delegation-meeting confirm eligibility). It is
    // prefilled from the loaded profile and its options come from the active-country list.
    [Fact]
    public void EditAccountForm_renders_the_nationality_picker_prefilled_from_the_profile()
    {
        var accountId = Guid.NewGuid();
        StubEditAccountFormLoads(accountId, hasAvatar: false, hasIdImage: false);

        var cut = RenderComponent<EditAccountForm>(p => p
            .Add(c => c.AccountId, accountId)
            .Add(c => c.Scope, "visitors")
            .Add(c => c.IsVisitorScope, true)
            .Add(c => c.ShowVipPhoto, false));

        Assert.Contains("Admin.Edit.Nationality", cut.Markup);
        Assert.Contains("Admin.Edit.Nationality.Hint", cut.Markup);
        Assert.Contains("Saudi Arabia (SA)", cut.Markup);
        Assert.Contains("Egypt (EG)", cut.Markup);
    }

    // The nationality field is optional, so a failed country read must never block the
    // rest of the edit (email / display name / tier / photos still render).
    [Fact]
    public void EditAccountForm_still_renders_when_the_country_list_cannot_be_read()
    {
        var accountId = Guid.NewGuid();
        var tier = VisitorTier("VIP");
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson",
                inv => inv.Arguments.Count > 0
                    && inv.Arguments[0] is string url && url.Contains("profile-types"))
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary> { tier }));
        JSInterop.Setup<ApiResult<AdminUserProfileView>>(
                "simfAccount.getJson",
                inv => inv.Arguments.Count > 0
                    && inv.Arguments[0] is string url && url.EndsWith("/profile"))
            .SetResult(ApiResult<AdminUserProfileView>.Ok(
                Profile(accountId, hasAvatar: false, hasIdImage: false, tier.Id)));
        // No stub for walk-in/countries — the strict JS runtime throws on that call.

        var cut = RenderComponent<EditAccountForm>(p => p
            .Add(c => c.AccountId, accountId)
            .Add(c => c.Scope, "visitors")
            .Add(c => c.IsVisitorScope, true)
            .Add(c => c.ShowVipPhoto, false));

        Assert.Contains("Admin.Edit.Email", cut.Markup);
        Assert.Contains("Admin.Edit.Photo.Section", cut.Markup);
        Assert.DoesNotContain("Admin.Edit.LoadFailed", cut.Markup);
    }

    [Fact]
    public void VipRegistration_hides_new_vip_and_edit_without_the_visitor_permissions()
    {
        // The base grants only the Administrator role, not the named Visitors
        // policies — so _canRegister / _canEdit resolve false.
        StubVipList();

        var cut = RenderComponent<VipRegistration>();

        Assert.DoesNotContain("Admin.Vips.Action.AddVip", cut.Markup);
        Assert.DoesNotContain("Admin.Users.Action.Edit", cut.Markup);
    }

    [Fact]
    public void VipRegistration_shows_new_vip_and_edit_with_the_visitor_permissions()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.RegisterOnsite),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Edit));
        StubVipList();

        var cut = RenderComponent<VipRegistration>();

        Assert.Contains("Admin.Vips.Action.AddVip", cut.Markup);
        Assert.Contains("Admin.Users.Action.Edit", cut.Markup);
    }
}
