using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
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
    public void Faq_add_is_hidden_from_a_view_only_holder()
    {
        // D-830 moved the refusal one step earlier. Before, a View-only holder got
        // the Add button, opened the dialog, and found no Save; now the Add button
        // is gone, because SimfDataGrid renders it and the grid gates it on
        // Faq.Create. Asserting the dialog is unreachable is the stronger claim —
        // there is no longer a form to fill in before being told no.
        Authorization.SetPolicies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<FaqManager>();

        Assert.Empty(cut.FindAll(AddButtonSelector));
        // The grid itself rendered — proving the absence is the gate and not an
        // empty page.
        Assert.Contains("simf-grid", cut.Markup);
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
    public void Faq_add_stays_hidden_for_an_EDIT_only_holder()
    {
        // The case a single blanket permission would have got wrong: Edit does not
        // grant Create. The holder keeps the Edit affordance and loses only Add,
        // which is the distinction the two separate grid parameters exist to make.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Faq.Edit));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<FaqManager>();

        Assert.Empty(cut.FindAll(AddButtonSelector));
        Assert.NotEmpty(cut.FindAll("button.simf-tbbtn[title='Grid.Edit']"));
    }

    [Fact]
    public void Rating_config_add_is_hidden_from_a_view_only_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.View));
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<RatingConfig>();

        // Three grids on this page (types / groups / questions), all three gated.
        Assert.Empty(cut.FindAll(AddButtonSelector));
        Assert.Contains("simf-grid", cut.Markup);
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

    // D-830 — the pending queues carry the same two actions twice: in the grid
    // toolbar (a grid built-in, gated by the grid's parameters) and per row (page
    // markup, gated here). Gating one and not the other would have left the screen
    // half-fixed, which reads as an oversight rather than a decision.

    [Fact]
    public void Pending_row_approve_and_reject_are_hidden_from_a_view_only_holder()
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View));
        StubPendingVisitorRow();

        var cut = RenderComponent<PendingVisitors>();

        cut.WaitForAssertion(() =>
        {
            // The row is there and the read-only profile drawer is still offered —
            // so the two absences below are the gate, not an empty queue.
            Assert.Contains("Admin.Pending.Action.View", cut.Markup);
            Assert.DoesNotContain("Admin.Pending.Action.Approve", cut.Markup);
            Assert.DoesNotContain("Admin.Pending.Action.Reject", cut.Markup);
        });
    }

    [Fact]
    public void Pending_row_approve_is_shown_to_an_approve_holder_without_reject()
    {
        // Approve and Reject are separate codes on the security team's queue, and
        // an approver is not automatically a rejecter.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.Approve));
        StubPendingVisitorRow();

        var cut = RenderComponent<PendingVisitors>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Admin.Pending.Action.Approve", cut.Markup);
            Assert.DoesNotContain("Admin.Pending.Action.Reject", cut.Markup);
        });
    }

    [Fact]
    public void Pending_others_drives_both_the_bulk_and_the_row_actions_from_the_Others_codes()
    {
        // D-834. Every button on /admin/others/pending acts on a PARTNER account, so
        // every one of them answers to Others.*. D-830 shipped the row pair on
        // Admins.* because that is what the API asked for, and the owner ruled that
        // split a bug: the permission catalogue had always mapped Others.Approve to
        // both /admin/others/{id}/approve and /admin/others/bulk-approve, and
        // ApproveOtherEndpoint / RejectOtherEndpoint had simply kept the policy line
        // they were copy-pasted with. The endpoints were corrected; this pins the CP.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Others.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Others.Approve),
            PermissionCatalog.PolicyFor(PermissionCatalog.Others.Reject));
        StubPendingOtherRow();

        var cut = RenderComponent<PendingOthers>();

        // Exact button text, not a substring of the markup: "…Action.Approve" is a
        // prefix-free match only by accident, and "…Action.BulkApprove" contains it.
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(ButtonsLabelled(cut, "Admin.Pending.Action.BulkApprove"));
            Assert.NotEmpty(ButtonsLabelled(cut, "Admin.Pending.Action.BulkReject"));
            Assert.NotEmpty(ButtonsLabelled(cut, "Admin.Pending.Action.Approve"));
            Assert.NotEmpty(ButtonsLabelled(cut, "Admin.Pending.Action.Reject"));
        });
    }

    [Fact]
    public void Pending_others_offers_nothing_to_a_holder_of_only_the_Admins_pair()
    {
        // The other half of D-834, and the reason it was a bug and not a quirk: while
        // the per-row routes gated on Admins.*, an admin whose job is approving
        // ADMINS could also approve PARTNER accounts one at a time — a tier they were
        // never granted. Others.View is granted here so the page itself renders;
        // without an Others action code no approve or reject button may appear.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Others.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Approve),
            PermissionCatalog.PolicyFor(PermissionCatalog.Admins.Reject));
        StubPendingOtherRow();

        var cut = RenderComponent<PendingOthers>();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(ButtonsLabelled(cut, "Admin.Pending.Action.BulkApprove"));
            Assert.Empty(ButtonsLabelled(cut, "Admin.Pending.Action.BulkReject"));
            Assert.Empty(ButtonsLabelled(cut, "Admin.Pending.Action.Approve"));
            Assert.Empty(ButtonsLabelled(cut, "Admin.Pending.Action.Reject"));
            // The read-only View survives: that is what Others.View bought.
            Assert.NotEmpty(ButtonsLabelled(cut, "Admin.Pending.Action.View"));
        });
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> ButtonsLabelled(
        IRenderedFragment cut, string label) =>
        cut.FindAll("button").Where(button => button.TextContent.Trim() == label).ToList();

    private void StubPendingOtherRow() => StubPendingVisitorRow();

    private void StubPendingVisitorRow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var query = new GridQuery();
        var row = new AdminPendingUserSummary(
            Guid.NewGuid(), "pending@simf.test", "Pending Person", SimfClock.Now);
        JSInterop.Setup<ApiResult<GridPage<AdminPendingUserSummary>>>(
                invocation => invocation.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(
                GridPage<AdminPendingUserSummary>.Of(new[] { row }, 1, query)));
    }
}
