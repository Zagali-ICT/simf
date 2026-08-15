// D-191 (bUnit harness reference) — behavior tests for the
// ProfileTypeForm sub-component (D-115 Create + Edit dialog;
// D-186 added the IsPartnerForm parameter that drives both the
// MobileAppRole picker visibility AND the IsVisitor=!IsPartnerForm
// payload sent on Create).
//
// These tests demonstrate the harness pattern for two of the most
// common CP behaviors:
//   (1) form-field rendering driven by a parent-supplied parameter
//       (here: IsPartnerForm gates the MobileAppRole picker);
//   (2) JS-interop POST payload assertions (the form calls
//       `simfAccount.postJson` and the test inspects the body).
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Tests;

public sealed class ProfileTypeFormTests : CpComponentTestBase
{
    [Fact]
    public void Audience_form_omits_the_MobileAppRole_picker()
    {
        // D-186: the MobileAppRole picker is only meaningful for
        // partner-side ProfileTypes (audience profile types resolve
        // to MobileAppRole.Visitor at JWT issue time regardless of
        // any per-row value). The form hides the picker when
        // IsPartnerForm=false.
        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, false));

        // Asserted via the localized label key — no MobileAppRole row.
        var labels = cut.FindAll(".simf-field__label").Select(e => e.TextContent.Trim());
        Assert.DoesNotContain("Admin.ProfileTypes.Field.MobileAppRole", labels);
    }

    [Fact]
    public void Partner_form_includes_the_MobileAppRole_picker()
    {
        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, true));

        var labels = cut.FindAll(".simf-field__label").Select(e => e.TextContent.Trim());
        Assert.Contains("Admin.ProfileTypes.Field.MobileAppRole", labels);
    }

    [Fact]
    public void Audience_form_offers_the_VIP_tier_toggle_and_the_partner_form_does_not()
    {
        // The VIP tier is an AUDIENCE notion (VVIP / VIP), the pool VIP-tier seat
        // self-reservation draws from — the mirror image of the Meet-People
        // toggle, which is partner-only. Gate the box the same way so a partner
        // type can never be marked VIP from the form.
        var audience = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, false));
        Assert.Contains(
            "Admin.ProfileTypes.Field.IsVipTier",
            audience.Markup);

        var partner = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, true));
        Assert.DoesNotContain(
            "Admin.ProfileTypes.Field.IsVipTier",
            partner.Markup);
    }

    [Fact]
    public void Audience_create_posts_the_ticked_VIP_tier_flag()
    {
        // The whole point of the field: until it was bound, the seeder was the
        // only writer of IsVipTier anywhere, so a CP-created type could never be
        // VIP. Pin that the ticked box actually reaches the wire.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var summary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "Platinum", "بلاتيني", "#E5E4E2",
            "Visitor", "None", IsActive: true, IsVisitor: true,
            IsAppRegisterable: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(summary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, false));

        cut.FindAll("input.simf-field__input")[0].Change("Platinum");
        cut.FindAll("input.simf-field__input")[1].Change("بلاتيني");
        // The VIP-tier box is the last checkbox on the audience form.
        cut.FindAll("input[type=checkbox]").Last().Change(true);
        cut.Find("form").Submit();

        var body = (AdminCreateProfileTypeRequest)handler.Invocations
            .Single().Arguments[1]!;
        Assert.True(body.IsVipTier);
    }

    [Fact]
    public void Audience_create_leaves_the_VIP_tier_flag_off_by_default()
    {
        // IsVipTier grants VIP-tier seat self-reservation, so an untouched form
        // must not send true.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var summary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "Plain", "عادي", "#64748B",
            "Visitor", "None", IsActive: true, IsVisitor: true,
            IsAppRegisterable: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(summary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, false));

        cut.FindAll("input.simf-field__input")[0].Change("Plain");
        cut.FindAll("input.simf-field__input")[1].Change("عادي");
        cut.Find("form").Submit();

        var body = (AdminCreateProfileTypeRequest)handler.Invocations
            .Single().Arguments[1]!;
        Assert.False(body.IsVipTier);
    }

    [Fact]
    public void Edit_mode_prefills_the_VIP_tier_flag_and_sends_it_back()
    {
        // The update request defaults IsVipTier to false, so a form that did not
        // pre-fill and resend it would silently DEMOTE a VIP tier on any
        // unrelated edit — the way ShowInPartnerDirectory once failed open.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var existing = new AdminProfileTypeSummary(
            Guid.NewGuid(), "VIP", "كبار", "#FFD700",
            "Visitor", "None", IsActive: true, IsVisitor: true,
            IsAppRegisterable: true, ShowInPartnerDirectory: true,
            IsVipTier: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(existing));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.Initial, existing)
            .Add(p => p.IsPartnerForm, false));

        // Change something unrelated, then save.
        cut.FindAll("input.simf-field__input")[0].Change("VIP renamed");
        cut.Find("form").Submit();

        var body = (AdminUpdateProfileTypeRequest)handler.Invocations
            .Single().Arguments[1]!;
        Assert.True(body.IsVipTier);
    }

    [Fact]
    public void Audience_create_posts_IsVisitor_true_and_UserType_Visitor()
    {
        // D-190 review-loop: the form Create path sends
        //   UserType = "Visitor", IsVisitor = !IsPartnerForm
        // on every payload, regardless of which host page rendered
        // the form. This test pins the audience-side wire shape.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var summary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "VIP", "كبار", "#FFD700",
            "Visitor", "None", IsActive: true, IsVisitor: true,
            IsAppRegisterable: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(summary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, false));

        // Fill the required fields, then submit.
        cut.FindAll("input.simf-field__input")[0].Change("VIP");
        cut.FindAll("input.simf-field__input")[1].Change("كبار");
        cut.Find("form").Submit();

        // The first invocation MUST be the POST to /profile-types
        // with UserType="Visitor" and IsVisitor=true.
        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/profile-types", (string)posted.Arguments[0]!);
        var body = (AdminCreateProfileTypeRequest)posted.Arguments[1]!;
        Assert.Equal("Visitor", body.UserType);
        Assert.True(body.IsVisitor);
        Assert.Equal("VIP", body.Name);
    }

    [Fact]
    public void Partner_create_posts_IsVisitor_false()
    {
        // D-186: the partner-side host (OtherProfileTypesList)
        // passes IsPartnerForm=true; the form sends IsVisitor=false
        // so the new row lands on the CP Others approval queue.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var summary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "Sponsor", "راعي", "#8B5CF6",
            "Visitor", "None", IsActive: true, IsVisitor: false,
            IsAppRegisterable: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(summary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, true));

        cut.FindAll("input.simf-field__input")[0].Change("Sponsor");
        cut.FindAll("input.simf-field__input")[1].Change("راعي");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        var body = (AdminCreateProfileTypeRequest)posted.Arguments[1]!;
        Assert.Equal("Visitor", body.UserType);
        Assert.False(body.IsVisitor);
    }

    [Fact]
    public void Selecting_the_Staff_app_role_auto_hides_the_type_from_the_app_picker()
    {
        // D-725: the create form mirrors the seeder — picking an operational
        // app role (Staff / Moderator) auto-unchecks "Show in the app sign-up
        // picker", so a hand-created CP-only type is hidden from mobile
        // self-registration just like the seeded Staff / Moderator rows.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var summary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "Gate operator", "مشغّل", "#244A77",
            "Visitor", "Staff", IsActive: true, IsVisitor: false,
            IsAppRegisterable: false);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(summary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.IsPartnerForm, true));

        cut.FindAll("input.simf-field__input")[0].Change("Gate operator");
        cut.FindAll("input.simf-field__input")[1].Change("مشغّل");
        // Pick the operational Staff role from the Mobile-app role select.
        cut.Find("select.simf-field__select").Change("Staff");
        cut.Find("form").Submit();

        var body = (AdminCreateProfileTypeRequest)handler.Invocations.Single().Arguments[1]!;
        Assert.Equal("Staff", body.MobileAppRole);
        // The box was auto-unchecked when Staff was picked.
        Assert.False(body.IsAppRegisterable);
    }

    [Fact]
    public void Edit_mode_preserves_existing_IsVisitor_on_PUT()
    {
        // D-187 review-pass discovered: the route-bound
        // UpdateAdminProfileTypeRouteRequest was missing IsVisitor;
        // the form's PUT payload now carries it explicitly. Edit
        // mode reads Initial.IsVisitor and round-trips it on the
        // update so the row's audience/partner scope stays put
        // unless the admin deliberately flips it on the form.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var partnerSummary = new AdminProfileTypeSummary(
            Guid.NewGuid(), "Exhibitor", "عارض", "#10B981",
            "Visitor", "Staff", IsActive: true, IsVisitor: false,
            IsAppRegisterable: true);
        var handler = JSInterop.Setup<ApiResult<AdminProfileTypeSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminProfileTypeSummary>.Ok(partnerSummary));

        var cut = RenderComponent<ProfileTypeForm>(parameters => parameters
            .Add(p => p.Initial, partnerSummary));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal(
            $"/account/api/admin/profile-types/{partnerSummary.Id}",
            (string)put.Arguments[0]!);
        var body = (AdminUpdateProfileTypeRequest)put.Arguments[1]!;
        Assert.False(body.IsVisitor); // preserved from Initial.IsVisitor
    }
}
