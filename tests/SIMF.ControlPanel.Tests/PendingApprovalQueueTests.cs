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
        new(Guid.NewGuid(), "pending@simf.test", "Pending Person", DateTimeOffset.UtcNow);

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
            // Staff approve directly — the review View action is others/visitors only.
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
}
