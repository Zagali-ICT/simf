// D-191 (bUnit harness reference) — behavior tests for the
// MeetingRequestsList page (D-174 backend; D-183 CP UI; D-185 split
// AdminMeetingRequestRow vs AdminMeetingRequestDetail + on-click
// detail fetch via simfAccount.getJson).
//
// These tests demonstrate the harness pattern for:
//   (1) page-init JS-interop POST (the list-load shape);
//   (2) filter-change mutate-in-place that pins both the filter
//       key AND Skip=0 (the D-183 review-pass discipline);
//   (3) modal-open with background detail-fetch on row click (the
//       D-185 PII-split pattern).
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class MeetingRequestsListTests : CpComponentTestBase
{
    private static AdminMeetingRequestRow Pending(Guid id, string requester) =>
        new(id, SessionId: Guid.NewGuid(), SessionCode: "SES-001",
            SessionTitle: "Cyber", RequestedByUserId: Guid.NewGuid(),
            RequesterName: requester, Subject: "Hello",
            Status: MeetingRequestStatus.Pending, ResponseNote: null,
            CreatedAt: DateTimeOffset.UtcNow,
            RespondedAt: null);

    [Fact]
    public void Page_load_posts_the_list_query_with_Top_50()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var emptyPage = new GridPage<AdminMeetingRequestRow>();
        var listHandler = JSInterop.Setup<ApiResult<GridPage<AdminMeetingRequestRow>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminMeetingRequestRow>>.Ok(emptyPage));

        var cut = RenderComponent<MeetingRequestsList>();

        // OnInitializedAsync is async — wait for the JS call to land
        // before inspecting the handler's invocation list.
        cut.WaitForAssertion(() => Assert.Single(listHandler.Invocations));
        var posted = listHandler.Invocations.Single();
        Assert.Equal("/account/api/admin/meeting-requests/list",
            (string)posted.Arguments[0]!);
        var body = (GridQuery)posted.Arguments[1]!;
        Assert.Equal(50, body.Top);
        Assert.Equal(0, body.Skip);
        Assert.Empty(body.Filters); // no filter on first load
    }

    [Fact]
    public void Status_filter_change_mutates_query_in_place_resets_Skip_and_reloads()
    {
        // D-183 review-pass: the filter-change handler MUST mutate
        // the existing _query (preserving any future Sort / Search
        // state) AND reset Skip to 0 (filter change resets paging).
        // The list endpoint is called again with the new filter.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var emptyPage = new GridPage<AdminMeetingRequestRow>();
        var listHandler = JSInterop.Setup<ApiResult<GridPage<AdminMeetingRequestRow>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminMeetingRequestRow>>.Ok(emptyPage));

        var cut = RenderComponent<MeetingRequestsList>();
        // First call = initial page load (wait for async OnInit).
        cut.WaitForAssertion(() => Assert.Single(listHandler.Invocations));

        // Trigger the status filter change to "Pending".
        cut.Find("select.simf-field__input").Change("Pending");

        // Second call carries Filters["status"]="Pending" + Skip=0.
        cut.WaitForAssertion(() => Assert.Equal(2, listHandler.Invocations.Count));
        var second = listHandler.Invocations.Last();
        var body = (GridQuery)second.Arguments[1]!;
        Assert.Equal("Pending", body.Filters["status"]);
        Assert.Equal(0, body.Skip);
    }

    [Fact]
    public async Task Respond_button_opens_modal_then_fetches_detail_with_email()
    {
        // D-185 PII-split: clicking Respond opens the modal with the
        // row data immediately (email=null) AND fires a detail-fetch
        // to GET /admin/meeting-requests/{id} which returns the
        // requester email. The modal swaps to the detail payload
        // when the fetch resolves.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var rowId = Guid.NewGuid();
        var row = Pending(rowId, "Visitor One");
        var listPage = new GridPage<AdminMeetingRequestRow>
        {
            Items = new[] { row },
            Total = 1,
            Skip = 0,
            Top = 50,
        };
        JSInterop.Setup<ApiResult<GridPage<AdminMeetingRequestRow>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminMeetingRequestRow>>.Ok(listPage));

        var detail = new AdminMeetingRequestDetail(
            rowId, row.SessionId, row.SessionCode, row.SessionTitle,
            row.RequestedByUserId, row.RequesterName,
            RequesterEmail: "visitor.one@simf.test",
            row.Subject, row.Status, row.ResponseNote,
            row.CreatedAt, row.RespondedAt);
        var detailHandler = JSInterop.Setup<ApiResult<AdminMeetingRequestDetail>>(
            inv => inv.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<AdminMeetingRequestDetail>.Ok(detail));

        var cut = RenderComponent<MeetingRequestsList>();

        // Wait for the initial list-load to render the table row
        // (the table appears only after _loading flips back to false).
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("table tbody tr")));

        // Click the row's Respond button.
        await cut.InvokeAsync(() =>
            cut.Find("table tbody tr button").Click());

        // The detail GET was issued with the row's id.
        cut.WaitForAssertion(() => Assert.Single(detailHandler.Invocations));
        var fetched = detailHandler.Invocations.Single();
        Assert.Equal(
            $"/account/api/admin/meeting-requests/{rowId}",
            (string)fetched.Arguments[0]!);

        // After the detail resolves the modal renders the email
        // under the requester name (simf-table__sub class).
        cut.WaitForAssertion(() =>
        {
            var email = cut.Find("dd .simf-table__sub").TextContent.Trim();
            Assert.Equal("visitor.one@simf.test", email);
        });
    }
}
