// DEF-CHK-003 — the hall-arrivals console's session picker applied the ARRIVAL
// window (EnsureSessionLiveNow, ± 15 min) to BOTH actions, so a session that had
// ended dropped out of the list and its hall could never be checked OUT — exactly
// when an operator needs to. The server only windows the arrival; the departures
// endpoint has no window at all, so a session that has already opened for arrivals
// stays selectable. These bUnit tests pin that: an ended session is still offered,
// a live one is offered and listed first, and a not-yet-started one is not.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class HallArrivalsConsoleSessionPickerTests : CpComponentTestBase
{
    private static AdminSessionSummary Session(
        string code, string title, int startOffsetMin, int endOffsetMin, bool isActive = true)
    {
        var now = DateTimeOffset.UtcNow;
        return new AdminSessionSummary(
            Guid.NewGuid(), code, title, title,
            Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
            now.AddMinutes(startOffsetMin), now.AddMinutes(endOffsetMin), 100, isActive,
            now);
    }

    // Stubs the console's session-list call with the supplied rows and grants the
    // Record permission the AuthorizedAction block gates on.
    private void Arrange(params AdminSessionSummary[] rows)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.Record));
        var page = GridPage<AdminSessionSummary>.Of(rows, rows.Length, new GridQuery());
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminSessionSummary>>.Ok(page));
    }

    [Fact]
    public void A_session_that_ended_hours_ago_is_still_selectable_for_check_out()
    {
        // Ended 3 hours ago — far past the ±15 min arrival grace that used to hide it.
        var ended = Session("SES-ENDED", "Closed Plenary", -240, -180);
        Arrange(ended);

        var cut = RenderComponent<HallArrivalsConsole>();

        cut.WaitForAssertion(() => Assert.Contains(ended.Title, cut.Markup));
        // The check-out action is available for it (the QR field + both buttons render).
        Assert.Contains("Admin.HallArrivals.Action.CheckOut", cut.Markup);
    }

    [Fact]
    public void A_session_that_has_not_opened_for_arrivals_yet_is_not_offered()
    {
        // Starts in an hour — no attendance row can exist for it, so nothing to
        // check in or out; the picker keeps hiding it.
        var future = Session("SES-FUTURE", "Tomorrow Keynote", 60, 120);
        Arrange(future);

        var cut = RenderComponent<HallArrivalsConsole>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.HallArrivals.NoSessions", cut.Markup));
        Assert.DoesNotContain(future.Title, cut.Markup);
    }

    [Fact]
    public void An_inactive_session_is_not_offered()
    {
        var inactive = Session("SES-OFF", "Cancelled Panel", -15, 45, isActive: false);
        Arrange(inactive);

        var cut = RenderComponent<HallArrivalsConsole>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.HallArrivals.NoSessions", cut.Markup));
        Assert.DoesNotContain(inactive.Title, cut.Markup);
    }

    [Fact]
    public void The_live_session_is_listed_before_an_ended_one()
    {
        var ended = Session("SES-ENDED", "Closed Plenary", -240, -180);
        var live = Session("SES-LIVE", "Running Panel", -15, 45);
        // The API returns them sorted by start, so the ended one arrives first.
        Arrange(ended, live);

        var cut = RenderComponent<HallArrivalsConsole>();

        cut.WaitForAssertion(() => Assert.Contains(live.Title, cut.Markup));
        Assert.Contains(ended.Title, cut.Markup);
        Assert.True(
            cut.Markup.IndexOf(live.Title, StringComparison.Ordinal)
            < cut.Markup.IndexOf(ended.Title, StringComparison.Ordinal),
            "The live session must be offered before an ended one.");
    }
}
