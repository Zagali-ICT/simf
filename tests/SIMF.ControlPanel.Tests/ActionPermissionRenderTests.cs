using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

/// <summary>
/// D-828 — the four pages that gained an action gate, asserted from the outside:
/// a holder of only <c>X.View</c> does not get the button.
///
/// <para>The ratchet next door proves the markup contains an
/// <c>&lt;AuthorizedAction&gt;</c>. That is a grep, and a grep cannot tell a
/// working gate from one wrapped around the wrong permission or rendered in a
/// branch that never executes. These render the real page under two identities
/// and compare what comes out, which is the only claim worth making: the button
/// is there for someone who may use it and absent for someone who may not.</para>
///
/// <para>Both directions matter equally. A gate that hides the button from
/// everyone is not a fix, it is an outage with good intentions — so every case
/// here asserts the permitted identity still sees it.</para>
/// </summary>
public sealed class ActionPermissionRenderTests : CpComponentTestBase
{
    /// <summary>Cancel is always the "secondary" variant, so the primary button in
    /// a dialog footer is the confirming action.</summary>
    private const string DialogSubmitSelector = ".simf-modal__footer button.simf-button--primary";

    private const string AddButtonSelector = "button.simf-tbbtn[title='Grid.Add']";

    [Fact]
    public void Faq_save_is_hidden_from_a_view_only_holder()
    {
        Authorization.SetPolicies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<FaqManager>();
        cut.Find(AddButtonSelector).Click();

        Assert.Empty(cut.FindAll(DialogSubmitSelector));
    }

    [Fact]
    public void Faq_save_is_shown_to_a_create_holder()
    {
        // The other half. The Add dialog opens in create mode, so Faq.Create is
        // the permission that should reveal it — not Faq.Edit, which is why the
        // gate reads the modal's own mode rather than picking one of the two.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Create));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<FaqManager>();
        cut.Find(AddButtonSelector).Click();

        Assert.NotEmpty(cut.FindAll(DialogSubmitSelector));
    }

    [Fact]
    public void Faq_save_stays_hidden_for_an_EDIT_only_holder_in_the_ADD_dialog()
    {
        // The case a single blanket permission would have got wrong: Edit does not
        // grant Create, and the Add dialog is a create.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Edit));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<FaqManager>();
        cut.Find(AddButtonSelector).Click();

        Assert.Empty(cut.FindAll(DialogSubmitSelector));
    }

    [Fact]
    public void Rating_config_save_is_hidden_from_a_view_only_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<RatingConfig>();
        cut.Find(AddButtonSelector).Click();

        Assert.Empty(cut.FindAll(DialogSubmitSelector));
    }

    [Fact]
    public void Rating_config_save_is_shown_to_a_create_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.Create));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<RatingConfig>();
        cut.Find(AddButtonSelector).Click();

        Assert.NotEmpty(cut.FindAll(DialogSubmitSelector));
    }

    [Fact]
    public void Operations_save_is_hidden_from_a_view_only_holder()
    {
        // Both loads are mocked so the page reaches its LOADED state. Without
        // that it renders the error branch, where there is no Save button for a
        // reason that has nothing to do with permissions — and the test would
        // pass while proving nothing.
        Authorization.SetPolicies(PermissionCatalog.PolicyFor(PermissionCatalog.Operations.View));
        StubOperationsLoads();

        var cut = RenderComponent<OperationsToggles>();

        Assert.Empty(cut.FindAll(".simf-form__actions button.simf-button--primary"));
        // The page itself still rendered — proving the absence above is the gate
        // and not a failed load.
        Assert.Contains("Admin.Operations.RegistrationGate.IsOpen", cut.Markup);
    }

    [Fact]
    public void Operations_save_is_shown_to_an_edit_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Operations.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Operations.Edit));
        StubOperationsLoads();

        var cut = RenderComponent<OperationsToggles>();

        // Two sections, two Save buttons.
        Assert.Equal(2, cut.FindAll(".simf-form__actions button.simf-button--primary").Count);
    }

    private void StubOperationsLoads()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<RegistrationGateState>>(
                "simfAccount.getJson",
                invocation => ((string)invocation.Arguments[0]!).Contains("registration-gate"))
            .SetResult(ApiResult<RegistrationGateState>.Ok(
                new RegistrationGateState(true, null, new DateTime(2026, 8, 2, 9, 0, 0), null)));
        JSInterop.Setup<ApiResult<ArchiveVisibilityState>>(
                "simfAccount.getJson",
                invocation => ((string)invocation.Arguments[0]!).Contains("archive/visibility"))
            .SetResult(ApiResult<ArchiveVisibilityState>.Ok(
                new ArchiveVisibilityState(true, new DateTime(2026, 8, 2, 9, 0, 0), null)));
    }

    [Fact]
    public void Vip_notify_trigger_is_hidden_from_a_view_only_holder()
    {
        // The worst of the four before the fix: this button opens a flow that asks
        // the operator to pick recipients and write a bilingual message before the
        // API refuses it. Gating the TRIGGER is what makes that impossible rather
        // than merely futile.
        Authorization.SetPolicies(PermissionCatalog.PolicyFor(PermissionCatalog.Vips.View));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<VipsList>();

        Assert.Empty(cut.FindAll("button.simf-tbbtn[title^='Admin.Vips.NotifySelected']"));
    }

    [Fact]
    public void Vip_notify_trigger_is_shown_to_a_notify_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Vips.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Vips.Notify));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<VipsList>();

        Assert.NotEmpty(cut.FindAll("button.simf-tbbtn[title^='Admin.Vips.NotifySelected']"));
    }
}
