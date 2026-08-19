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
using SIMF.Common.Options;
using SIMF.Contracts.Sessions;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class HallArrivalsConsoleSessionPickerTests : CpComponentTestBase
{
    private static HallArrivalSessionOption Session(
        string code, string title, int startOffsetMin, int endOffsetMin, bool isActive = true)
    {
        var now = SimfClock.Now;
        // isActive is no longer a field: the endpoint scopes itself to active
        // sessions server-side, so an inactive one never reaches the picker.
        _ = isActive;
        return new HallArrivalSessionOption(
            Guid.NewGuid(), code, title, title,
            "Main Hall", "القاعة الرئيسية",
            now.AddMinutes(startOffsetMin), now.AddMinutes(endOffsetMin),
            WalkInModeOptions.DefaultArrivalGraceMinutes);
    }

    // Stubs the console's session-list call with the supplied rows and grants the
    // Record permission the AuthorizedAction block gates on.
    private void Arrange(params HallArrivalSessionOption[] rows)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.HallArrivals.Record));
        var page = GridPage<HallArrivalSessionOption>.Of(rows, rows.Length, new GridQuery());
        JSInterop.Setup<ApiResult<GridPage<HallArrivalSessionOption>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<HallArrivalSessionOption>>.Ok(page));
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

    /// <summary>An inactive session is excluded SERVER-side now, not here.
    ///
    /// <para>This used to assert the console filtered <c>IsActive</c> off its own
    /// rows. The console no longer receives that field: it reads
    /// <c>/admin/hall-arrivals/sessions/list</c>, which applies
    /// <c>Where(session =&gt; session.IsActive)</c> as the resource's own scope,
    /// ahead of the grid filters and where no request can widen it. So the
    /// guarantee did not disappear with the field, it moved to the one place a
    /// client cannot override - and this test moved with it, to
    /// <c>SIMF.Api.Tests/ConsoleRoleReachabilityTests</c>,
    /// <c>The_arrival_picker_is_scoped_to_active_sessions</c>, which seeds one
    /// active and one inactive session in the same hall and both inside the
    /// arrival window, so only <c>IsActive</c> separates them.</para>
    ///
    /// <para>Kept as a named, skipped fact rather than deleted, because a reader
    /// who remembers this rule should find where it went instead of concluding
    /// nothing checks it.</para></summary>
    [Fact(Skip = "Moved server-side: the endpoint scopes to active sessions, and "
                 + "the console no longer receives IsActive to filter on.")]
    public void An_inactive_session_is_not_offered()
    {
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
