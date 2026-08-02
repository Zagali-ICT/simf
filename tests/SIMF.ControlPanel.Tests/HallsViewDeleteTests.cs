// D-353 rollout â€” behaviour tests for HallsViewDelete (merged View/Delete
// form). View shows details only; Delete mode gates the soft-delete behind
// SimfConfirm (no DELETE until confirmed).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class HallsViewDeleteTests : CpComponentTestBase
{
    private static AdminHallDetail Detail() => new(
        Guid.NewGuid(), "HALL-A", "Main Hall", "Ø§Ù„Ù‚Ø§Ø¹Ø© Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠØ©", 250, "Ground",
        EquipmentNotes: "Projector + PA", IsActive: true,
        DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Detail()));

        Assert.Contains("Main Hall", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-button--danger"));
        Assert.Empty(cut.FindAll(".simf-modal"));
    }

    [Fact]
    public void Delete_mode_gates_the_call_behind_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var row = Detail();
        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/halls/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Detail()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }

    // -- QA B16: the hall-side occupancy view -------------------------------
    //
    // Before B16 no hall surface listed what the hall was doing, so the
    // session/booking overlap rule only ever surfaced as a 409. The detail form
    // now reads the hall-gated schedule endpoint and renders the assigned
    // sessions with their LOCAL (Saudi, 12-hour) times and status.

    // 11:00 on the forum's opening day. Stored AND rendered as 11:00 — owner
    // decision 2026-07-31, Saudi local storage with no conversion on the way out.
    // This constant used to be 08:00Z precisely because the render added +3.
    private static readonly DateTime ScheduleStart =
        new(2026, 1, 5, 11, 0, 0);

    private static AdminSessionSummary ScheduledSession(Guid hallId) => new(
        Guid.NewGuid(), "SES-1", "Opening Plenary", "SES-1-AR",
        hallId, "Main Hall", "MAIN-HALL-AR",
        ScheduleStart, ScheduleStart.AddHours(1),
        Capacity: 250, IsActive: true, CreatedAt: DateTime.UnixEpoch);

    // Plans the schedule read; returns the handler so a test can assert the URL.
    private static ApiResult<GridPage<AdminSessionSummary>> SchedulePage(
        params AdminSessionSummary[] sessions) =>
        ApiResult<GridPage<AdminSessionSummary>>.Ok(
            GridPage<AdminSessionSummary>.Of(sessions, sessions.Length, 0, 200));

    [Fact]
    public void B16_schedule_lists_the_sessions_assigned_to_this_hall()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var schedule = JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(SchedulePage(ScheduledSession(row.Id)));

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        // It reads the hall's own schedule endpoint (Halls.View-gated), not the
        // sessions grid.
        var call = Assert.Single(schedule.Invocations);
        Assert.Equal($"/account/api/admin/halls/{row.Id}/schedule",
            (string)call.Arguments[0]!);

        Assert.Contains("Admin.Halls.Schedule.Heading", cut.Markup);
        Assert.Contains("SES-1", cut.Markup);
        Assert.Contains("Opening Plenary", cut.Markup);
        Assert.Contains("Admin.Sessions.Status.Scheduled", cut.Markup);
    }

    [Fact]
    public void B16_schedule_times_are_local_never_utc()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(SchedulePage(ScheduledSession(row.Id)));

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        // 12-hour Saudi render of the stored value, verbatim. The DoesNotContain
        // is the load-bearing half: 08:00 is what a reintroduced a zoned value-to-Saudi
        // conversion would produce from an 11:00 stored value, so its absence is
        // what proves nothing shifts on the way to the page.
        Assert.Contains("05-01-2026 11:00 AM", cut.Markup);
        Assert.Contains("05-01-2026 12:00 PM", cut.Markup);
        Assert.DoesNotContain("08:00", cut.Markup);
        Assert.DoesNotContain("02:00 PM", cut.Markup);
    }

    [Fact]
    public void B16_schedule_shows_the_empty_state_for_an_unbooked_hall()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(SchedulePage());

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        Assert.Contains("Admin.Halls.Schedule.None", cut.Markup);
    }

    [Fact]
    public void B16_a_capped_schedule_says_so_instead_of_reading_as_complete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        // The endpoint caps the page at 200 rows; Total is the unclamped count.
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminSessionSummary>>.Ok(
                GridPage<AdminSessionSummary>.Of(
                    [ScheduledSession(row.Id)], total: 213, skip: 0, top: 200)));

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        Assert.Contains("Admin.Halls.Schedule.Capped", cut.Markup);
    }

    [Fact]
    public void B16_a_complete_schedule_shows_no_capped_notice()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(SchedulePage(ScheduledSession(row.Id)));

        var cut = RenderComponent<HallsViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, row));

        Assert.DoesNotContain("Admin.Halls.Schedule.Capped", cut.Markup);
    }
}
