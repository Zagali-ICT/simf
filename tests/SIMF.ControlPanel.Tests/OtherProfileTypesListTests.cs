// D-191 (bUnit harness reference) — behavior test for the
// OtherProfileTypesList page (D-115 list page; D-186 routed the
// partner-side queue through the new IsVisitor=false filter).
//
// This is the canonical "filter-pinned list page" pattern. The page
// loads the list on init with both filters set; any future grid-
// state change goes through OnQueryChangedAsync which MUST re-pin
// both filters every time (the grid drops filter keys on sort /
// page changes if the page doesn't re-apply them).
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

namespace SIMF.ControlPanel.Tests;

public sealed class OtherProfileTypesListTests : CpComponentTestBase
{
    [Fact]
    public void Initial_load_pins_both_userType_Visitor_and_isVisitor_false_filters()
    {
        // D-186: the OtherProfileTypesList page renders the partner-
        // side ProfileTypes queue. Every list call MUST carry both
        // filter keys — `userType=Visitor` (post-D-186 every non-
        // admin row lives under the Visitor scope) AND
        // `isVisitor=false` (the partner sub-scope). Dropping
        // either filter would surface audience rows on the Others
        // page or partner rows on the Visitors page, breaking the
        // queue-isolation invariant the CP renders.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var emptyPage = new GridPage<AdminProfileTypeSummary>();
        var listHandler = JSInterop.Setup<ApiResult<GridPage<AdminProfileTypeSummary>>>(
            inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminProfileTypeSummary>>.Ok(emptyPage));

        var cut = RenderComponent<OtherProfileTypesList>();

        cut.WaitForAssertion(() => Assert.Single(listHandler.Invocations));
        var posted = listHandler.Invocations.Single();
        var body = (GridQuery)posted.Arguments[1]!;
        Assert.Equal("Visitor", body.Filters["userType"]);
        Assert.Equal("false", body.Filters["isVisitor"]);
        Assert.Equal(20, body.Top); // page-default
    }
}
