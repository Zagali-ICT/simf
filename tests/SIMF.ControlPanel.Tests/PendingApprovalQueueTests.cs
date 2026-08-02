// Tests: the D-641 PendingApprovalPageBase extraction. The three pending-approval
// queues (admins / others / visitors) now share their grid-load + approve/reject/
// bulk logic through PendingApprovalPageBase; these bUnit tests prove each page
// still renders its rows + its divergent RowActions through the base, and that the
// inherited reject flow opens the shared reject modal.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class PendingApprovalQueueTests : CpComponentTestBase
{
    private static readonly AdminPendingUserSummary Row =
        new(Guid.NewGuid(), "pending@simf.test", "Pending Person", SimfClock.Now);

    // Every queue loads its rows in OnInitializedAsync via the base LoadAsync ->
    // simfAccount.postJson({apiBase}/pending/list). One loose stub covers all three.
    private void StubPendingList()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var page = GridPage<AdminPendingUserSummary>.Of(new[] { Row }, 1, new GridQuery());
        JSInterop.Setup<ApiResult<GridPage<AdminPendingUserSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminPendingUserSummary>>.Ok(page));
    }

    [Fact]
    public void Staff_queue_renders_rows_with_direct_approve_and_reject_only()
    {
        StubPendingList();

        var cut = RenderComponent<PendingStaff>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("pending@simf.test", cut.Markup);
            Assert.Contains("Pending Person", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Approve", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Reject", cut.Markup);
            // The staff queue confirms instead of reviewing — the profile-review
            // View action is others/visitors only (D-128 / D-809).
            Assert.DoesNotContain("Admin.Pending.Action.View", cut.Markup);
        });
    }

    [Fact]
    public void Others_queue_renders_view_approve_reject_actions()
    {
        StubPendingList();

        var cut = RenderComponent<PendingOthers>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("pending@simf.test", cut.Markup);
            Assert.Contains("Admin.Pending.Action.View", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Approve", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Reject", cut.Markup);
        });
    }

    [Fact]
    public void Visitors_queue_renders_view_approve_reject_actions()
    {
        StubPendingList();

        var cut = RenderComponent<PendingVisitors>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("pending@simf.test", cut.Markup);
            Assert.Contains("Admin.Pending.Action.View", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Approve", cut.Markup);
            Assert.Contains("Admin.Pending.Action.Reject", cut.Markup);
        });
    }

    [Fact]
    public void Reject_row_action_opens_the_inherited_shared_reject_modal()
    {
        StubPendingList();
        var cut = RenderComponent<PendingStaff>();
        cut.WaitForAssertion(() => Assert.Contains("Admin.Pending.Action.Reject", cut.Markup));

        // The row Reject button runs the base-class OpenReject, which surfaces the
        // shared reject modal (Admin.Pending.Reject.Title) — proving the inherited
        // reject state + modal render through the derived page.
        var rejectButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Admin.Pending.Action.Reject"));
        rejectButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Pending.Reject.Title", cut.Markup));
    }

    // D-809 regression — approving an administrator-typed account grants Control
    // Panel access and mints the QR badge. It used to commit on the first click,
    // while rejecting had always demanded a typed reason. These two tests pin the
    // guard: the approve POST must not leave the browser until the admin confirms.
    private bool PostedTo(string fragment) =>
        JSInterop.Invocations["simfAccount.postJson"].Any(call => call.Arguments.Any(
            arg => arg is string url && url.Contains(fragment, StringComparison.Ordinal)));

    [Fact]
    public void Staff_row_approve_confirms_before_posting()
    {
        StubPendingList();
        var cut = RenderComponent<PendingStaff>();
        cut.WaitForAssertion(() => Assert.Contains("Admin.Pending.Action.Approve", cut.Markup));

        cut.FindAll("button")
           .First(b => b.TextContent.Contains("Admin.Pending.Action.Approve"))
           .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Pending.Approve.Confirm.Title", cut.Markup));
        Assert.False(PostedTo("/approve"), "approve must not post before the admin confirms");

        cut.FindAll(".simf-modal__footer .simf-button--primary")[^1].Click();

        cut.WaitForAssertion(() =>
            Assert.True(PostedTo("/approve"), "confirming must post the approval"));
    }

    // Bulk approve confirms on every queue — the guard lives in the shared base.
    private void AssertBulkApproveConfirms<TQueue>() where TQueue : Microsoft.AspNetCore.Components.IComponent
    {
        StubPendingList();
        var cut = RenderComponent<TQueue>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Pending.Action.BulkApprove", cut.Markup));

        cut.FindAll("input[type=checkbox]").Last().Change(true);
        cut.FindAll("button")
           .First(b => b.TextContent.Contains("Admin.Pending.Action.BulkApprove"))
           .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.Pending.BulkApprove.Confirm.Title", cut.Markup));
        Assert.False(PostedTo("bulk-approve"),
            "bulk approve must not post before the admin confirms");

        // The commit path was REWRITTEN by D-809 (OnBulkApproveAsync now stages,
        // ConfirmBulkApproveAsync does the work). Assert the worker is reachable,
        // or a guard wired to Cancel would leave this test green.
        cut.FindAll(".simf-modal__footer .simf-button--primary")[^1].Click();

        cut.WaitForAssertion(() =>
            Assert.True(PostedTo("bulk-approve"), "confirming must post the bulk approval"));
    }

    [Fact]
    public void Staff_bulk_approve_confirms_before_posting() =>
        AssertBulkApproveConfirms<PendingStaff>();

    [Fact]
    public void Others_bulk_approve_confirms_before_posting() =>
        AssertBulkApproveConfirms<PendingOthers>();

    [Fact]
    public void Visitors_bulk_approve_confirms_before_posting() =>
        AssertBulkApproveConfirms<PendingVisitors>();
}
