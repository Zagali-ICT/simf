// Tests: DEF-SEA-001 — the session seat plan released a held seat on a SINGLE
// click, with no confirmation and with no holder name anywhere on the page: an
// admin could not tell whose seat they were taking, and could not undo it.
// These guard both halves of the fix — the click only ARMS the release, and the
// confirmation names the holder — so a future edit that wires the seat button
// straight back to the DELETE fails the build.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionSeatPlanReleaseConfirmTests : CpComponentTestBase
{
    private static readonly Guid HallId = Guid.NewGuid();

    private static readonly AdminSessionSummary PlannedSession = new(
        Guid.NewGuid(), "S-SEAT", "Opening", "S-SEAT-AR",
        HallId, "Main Hall", "MAIN-HALL-AR",
        DateTime.UnixEpoch, DateTime.UnixEpoch.AddHours(1),
        Capacity: 30, IsActive: true, CreatedAt: DateTime.UnixEpoch);

    // One attendee holding A2 — the seat an admin would click to release.
    private static readonly SeatPlanCell HeldSeat = new(
        Guid.NewGuid(), "A", 2, SeatReservationKind.UserBooking,
        BookingStatus.Approved, CheckedIn: false, HolderUserId: Guid.NewGuid(),
        HolderName: "Faisal Al-Harbi", HolderNameArabic: "فيصل الحربي",
        GuestHint: null, GuestHintArabic: null);

    private IRenderedComponent<SessionSeatPlan> RenderPlan()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));

        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.postJson",
            call => call.Arguments[0] is string url
                && url.EndsWith("/sessions/list", StringComparison.Ordinal))
            .SetResult(ApiResult<GridPage<AdminSessionSummary>>.Ok(
                GridPage<AdminSessionSummary>.Of([PlannedSession], 1, 0, 200)));

        JSInterop.Setup<ApiResult<GridPage<SeatPlanCell>>>(
            "simfAccount.postJson",
            call => call.Arguments[0] is string url
                && url.EndsWith("/seats/list", StringComparison.Ordinal))
            .SetResult(ApiResult<GridPage<SeatPlanCell>>.Ok(
                GridPage<SeatPlanCell>.Of([HeldSeat], 1, 0, 500)));

        JSInterop.Setup<ApiResult<HallSeatLayoutSnapshot>>(
            "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<HallSeatLayoutSnapshot>.Ok(
                new HallSeatLayoutSnapshot(HallId, ["A"], 3, 3, 30)));

        var cut = RenderComponent<SessionSeatPlan>();
        cut.Find("select").Change(PlannedSession.Id.ToString());
        return cut;
    }

    private int ReleaseCalls() =>
        JSInterop.Invocations
            .Count(i => i.Identifier == "simfAccount.deleteJson");

    [Fact]
    public void DEF_SEA_001_clicking_a_held_seat_confirms_instead_of_releasing()
    {
        var cut = RenderPlan();

        // Seat A2 is the held one (buttons render 1..3 in row order).
        cut.FindAll("button.seatgrid__seat")[1].Click();

        Assert.Equal(0, ReleaseCalls());
        Assert.Contains(
            "Admin.SessionSeatPlans.Release.Title", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DEF_SEA_001_the_confirmation_names_the_seat_holder()
    {
        var cut = RenderPlan();

        cut.FindAll("button.seatgrid__seat")[1].Click();

        // The admin must be told WHOSE seat this is before confirming.
        Assert.Equal("Faisal Al-Harbi",
            cut.Find("[data-testid='release-holder']").TextContent.Trim());
    }

    [Fact]
    public void DEF_SEA_001_cancelling_the_confirmation_keeps_the_seat()
    {
        var cut = RenderPlan();
        cut.FindAll("button.seatgrid__seat")[1].Click();

        // The cancel button is the secondary action in the dialog footer.
        cut.Find("button.simf-button--secondary").Click();

        Assert.Equal(0, ReleaseCalls());
        Assert.DoesNotContain(
            "release-holder", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DEF_SEA_001_the_plan_names_every_holder_without_hovering()
    {
        var cut = RenderPlan();

        // The roster under the grid answers "who is in which seat" at a glance —
        // the grid alone only carried colours.
        Assert.Contains("Faisal Al-Harbi", cut.Markup, StringComparison.Ordinal);
    }
}
