// Regression cover for the attendees roster's Kind (UserType) dropdown.
//
// The dropdown used to offer a third option, "Other". `UserType` collapsed to
// (Visitor, Admin) and "Other" stopped being a member, so picking it posted a
// filter the server could not honour: rather than 400 a shipped control the
// service quietly dropped the key, and the operator got the FULL roster back
// while the control still read "Others only". A silent no-op reads exactly like
// a working filter over a roster that happens to hold only visitors, which is
// why it survived. Nothing pinned the option list, so nothing failed when the
// enum member disappeared.
//
// These tests pin the two facts the page must keep:
//   * the Kind select offers exactly All + Visitors only, so a dead option
//     cannot be re-added without a red test;
//   * "All" means OMIT the key, never a sentinel the server has to special-case.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class AttendeesListKindFilterTests : CpComponentTestBase
{
    private const string ListCall = "simfAccount.postJson";

    [Fact]
    public void Kind_dropdown_offers_only_All_and_Visitors_only()
    {
        var cut = RenderAttendeesList();

        var values = string.Join(", ",
            cut.FindAll("#att-usertype option")
                .Select(option => $"'{option.GetAttribute("value")}'"));

        Assert.Equal("'', 'Visitor'", values);
    }

    [Fact]
    public void All_omits_the_userType_key_rather_than_posting_a_sentinel()
    {
        var cut = RenderAttendeesList();

        // Initial load: nothing chosen, so the key must be absent entirely.
        Assert.Equal(1, ListCallCount());
        Assert.False(LatestQuery().Filters.ContainsKey("userType"));

        Choose(cut, "Visitor");
        Assert.Equal(2, ListCallCount());
        Assert.Equal("Visitor", LatestQuery().Filters["userType"]);

        // Back to All. The key goes away rather than turning into "" or "All".
        Choose(cut, string.Empty);
        Assert.Equal(3, ListCallCount());
        Assert.False(LatestQuery().Filters.ContainsKey("userType"));
    }

    // ----------------------------------------------------------------------

    private IRenderedComponent<AttendeesList> RenderAttendeesList()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminAttendeeSummary>>>(
                invocation => invocation.Identifier == ListCall)
            .SetResult(ApiResult<GridPage<AdminAttendeeSummary>>.Ok(
                new GridPage<AdminAttendeeSummary>()));

        var cut = RenderComponent<AttendeesList>();
        cut.WaitForAssertion(() => Assert.Equal(1, ListCallCount()));
        return cut;
    }

    /// <summary>Pick a Kind value and apply it, the way an operator does.</summary>
    private static void Choose(IRenderedComponent<AttendeesList> cut, string value)
    {
        cut.Find("#att-usertype").Change(value);
        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Admin.Attendees.Filter.Apply"))
            .Click();
    }

    private int ListCallCount() => JSInterop.Invocations[ListCall].Count;

    /// <summary>The body of the most recent list call. Read it straight after the
    /// action under test: the page hands the SAME <see cref="GridQuery"/> instance
    /// to every call, so an earlier invocation is not a snapshot of what was sent
    /// at the time.</summary>
    private GridQuery LatestQuery() =>
        (GridQuery)JSInterop.Invocations[ListCall].Last().Arguments[1]!;
}
