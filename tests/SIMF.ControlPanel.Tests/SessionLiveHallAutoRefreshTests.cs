// Tests: QA B17 — the live per-session hall monitor used to issue its two reads
// only on session select and on a manual Refresh click: no timer, no polling, no
// push, so a door scan stayed invisible until an admin happened to click Refresh.
// These guard the auto-refresh contract AND its disposal — a PeriodicTimer left
// running on a Blazor Server circuit is a real leak, so the timer must exist only
// while a session is selected and must be gone after teardown.
using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Attendance;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionLiveHallAutoRefreshTests : CpComponentTestBase
{
    // The picker reads the ATTENDANCE session list, not the canonical Sessions
    // list: this page is gated Attendance.View, which the canonical list does not
    // accept. The row shape follows.
    private static readonly SessionAttendanceRow LiveSession = new(
        Guid.NewGuid(), "S-LIVE", "Live Session", "S-LIVE-AR",
        "Main Hall", "MAIN-HALL-AR",
        DateTime.UnixEpoch, DateTime.UnixEpoch.AddHours(1),
        TotalAttendees: 0, LiveNow: 0);

    // The poll loop's timer, observed through the field that owns it — the only
    // way to prove "started on selection / disposed on teardown" without waiting
    // out the real 15-second cadence.
    private static object? PollTimer(SessionLiveHall page) =>
        typeof(SessionLiveHall)
            .GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(page);

    private IRenderedComponent<SessionLiveHall> RenderMonitor()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        JSInterop.Setup<ApiResult<GridPage<SessionAttendanceRow>>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<SessionAttendanceRow>>.Ok(
                GridPage<SessionAttendanceRow>.Of([LiveSession], 1, 0, 200)));
        return RenderComponent<SessionLiveHall>();
    }

    [Fact]
    public void B17_no_session_selected_means_no_poll_timer()
    {
        var cut = RenderMonitor();

        Assert.Null(PollTimer(cut.Instance));
    }

    [Fact]
    public void B17_selecting_a_session_starts_the_poll_and_disposing_stops_it()
    {
        var cut = RenderMonitor();

        cut.Find("select").Change(LiveSession.SessionId.ToString());
        Assert.NotNull(PollTimer(cut.Instance));

        var page = cut.Instance;
        DisposeComponents();
        Assert.Null(PollTimer(page));
    }

    [Fact]
    public void B17_clearing_the_selection_stops_the_poll()
    {
        var cut = RenderMonitor();

        cut.Find("select").Change(LiveSession.SessionId.ToString());
        Assert.NotNull(PollTimer(cut.Instance));

        // Back to the placeholder option — nothing to monitor, so nothing polls.
        cut.Find("select").Change(string.Empty);
        Assert.Null(PollTimer(cut.Instance));
    }
}
