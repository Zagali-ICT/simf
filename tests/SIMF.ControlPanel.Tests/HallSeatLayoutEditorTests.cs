// A40 + B15 — the hall seat-layout editor.
//
// A40: the page was only reachable from the side menu with a blank hall picker
// and did NO client-side validation, so every rule (1–26 rows, 1–8 char labels,
// unique labels, 1–80 seats, layout <= hall capacity) was only discovered as a
// server 400 after a save. These tests pin the ?hallId= deep link the Halls grid
// row action navigates with, and that each rule now blocks Save with its own
// message.
//
// B15: a layout could never be removed. These pin the Remove button (only once a
// layout exists), the must-decide confirmation, and that the server's refusal
// text — which names how many reservations block it — is what the admin sees.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class HallSeatLayoutEditorTests : CpComponentTestBase
{
    private static readonly Guid HallId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static AdminHallSummary Hall(int capacity) =>
        new(HallId, "H-1", "Main Hall", "القاعة الرئيسية", capacity, "G",
            IsActive: true, DateTimeOffset.UtcNow);

    // Stubs the halls list + the layout read-back, and grants the three seat-layout
    // permissions the AuthorizedAction blocks gate on.
    private void Arrange(int hallCapacity, HallSeatLayoutSnapshot layout)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.SeatLayouts.View),
            PermissionCatalog.PolicyFor(PermissionCatalog.SeatLayouts.Edit),
            PermissionCatalog.PolicyFor(PermissionCatalog.SeatLayouts.Delete));

        var halls = new[] { Hall(hallCapacity) };
        JSInterop.Setup<ApiResult<GridPage<AdminHallSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminHallSummary>>.Ok(
                GridPage<AdminHallSummary>.Of(halls, halls.Length, new GridQuery())));
        JSInterop.Setup<ApiResult<HallSeatLayoutSnapshot>>(
                inv => inv.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<HallSeatLayoutSnapshot>.Ok(layout));
    }

    private static HallSeatLayoutSnapshot Layout(
        string[] rows, int[] counts, int hallCapacity) =>
        new(HallId, rows, counts.Length == 0 ? 0 : counts.Max(), counts.Sum(),
            hallCapacity, counts,
            rows.Select(_ => SeatTier.Normal).ToArray());

    // Renders the editor already deep-linked to the hall, exactly as the Halls grid
    // row action navigates (A40).
    private IRenderedComponent<HallSeatLayoutEditor> RenderDeepLinked()
    {
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/admin/halls/seat-layouts?hallId={HallId}");
        var cut = RenderComponent<HallSeatLayoutEditor>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.HallSeatLayouts.Save", cut.Markup));
        return cut;
    }

    [Fact]
    public void The_hallId_query_parameter_opens_the_editor_on_that_hall()
    {
        // A40 — without the deep link the admin landed on a blank picker; with it the
        // hall's rows are already loaded and editable.
        Arrange(hallCapacity: 100, Layout(["A", "B"], [10, 10], 100));

        var cut = RenderDeepLinked();

        // The loaded grid is on screen (the row-labels input carries the stored CSV).
        Assert.Equal("A,B", cut.Find("input[type=text]").GetAttribute("value"));
        // No rule is broken, so Save is live.
        Assert.False(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Duplicate_row_labels_block_save_client_side()
    {
        // A40 — mirrors the server's `rows.Distinct(OrdinalIgnoreCase)` check. Case
        // differences do NOT make a label unique, exactly as the server decides.
        Arrange(hallCapacity: 100, Layout(["A", "B"], [10, 10], 100));
        var cut = RenderDeepLinked();

        cut.Find("input[type=text]").Change("A,a");

        Assert.Contains("Admin.HallSeatLayouts.Validation.RowLabelDuplicate", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void A_row_label_longer_than_eight_characters_blocks_save_client_side()
    {
        // A40 — mirrors the server's `rows.Any(r => r.Length > 8)`.
        Arrange(hallCapacity: 100, Layout(["A"], [10], 100));
        var cut = RenderDeepLinked();

        cut.Find("input[type=text]").Change("BALCONY-LEFT");

        Assert.Contains("Admin.HallSeatLayouts.Validation.RowLabelLength", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void More_than_twenty_six_rows_blocks_save_client_side()
    {
        // A40 — mirrors the server's `rows.Count is < 1 or > 26`.
        Arrange(hallCapacity: 1000, Layout(["A"], [10], 1000));
        var cut = RenderDeepLinked();

        var twentySeven = string.Join(',',
            Enumerable.Range(1, 27).Select(i => $"R{i}"));
        cut.Find("input[type=text]").Change(twentySeven);

        Assert.Contains("Admin.HallSeatLayouts.Validation.RowCount", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Clearing_every_row_label_blocks_save_client_side()
    {
        // A40 — an empty layout is the server's 400 SEAT_LAYOUT_INVALID; the editor
        // now refuses it before the round-trip. (B15's Remove is the supported way
        // to end up with no layout.)
        Arrange(hallCapacity: 100, Layout(["A"], [10], 100));
        var cut = RenderDeepLinked();

        cut.Find("input[type=text]").Change(string.Empty);

        Assert.Contains("Admin.HallSeatLayouts.Validation.RowCount", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void A_seat_count_outside_one_to_eighty_blocks_save_client_side()
    {
        // A40 — mirrors the server's `requestedCounts.Any(c => c is < 1 or > 80)`.
        Arrange(hallCapacity: 1000, Layout(["A"], [10], 1000));
        var cut = RenderDeepLinked();

        cut.Find("input[type=number]").Change("81");

        Assert.Contains("Admin.HallSeatLayouts.Validation.SeatCount", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void A_layout_larger_than_the_hall_blocks_save_client_side()
    {
        // A40 — mirrors the server's `layoutCapacity > hall.Capacity` (which answers
        // 400 SEAT_CAPACITY_EXCEEDED).
        Arrange(hallCapacity: 12, Layout(["A"], [10], 12));
        var cut = RenderDeepLinked();

        cut.Find("input[type=number]").Change("40");

        Assert.Contains("Admin.HallSeatLayouts.Validation.Capacity", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void The_row_labels_input_max_length_matches_the_persisted_column()
    {
        // A40 — the UI MaxLength must equal the EF HasMaxLength (256 on
        // HallSeatLayout.RowLabels), so the admin cannot type an unsavable value.
        Arrange(hallCapacity: 100, Layout(["A"], [10], 100));
        var cut = RenderDeepLinked();

        Assert.Equal("256", cut.Find("input[type=text]").GetAttribute("maxlength"));
    }

    [Fact]
    public void A_hall_with_no_layout_yet_is_not_shouted_at()
    {
        // A40 — an un-laid-out hall has not FAILED anything; the preview placeholder
        // already says what to do. No error list, but Save stays disabled (an empty
        // layout is a server 400 — B15's Remove is the way to have no layout).
        Arrange(hallCapacity: 100, Layout([], [], 100));

        var cut = RenderDeepLinked();

        Assert.DoesNotContain("Admin.HallSeatLayouts.Validation.RowCount", cut.Markup);
        Assert.True(SaveButton(cut).HasAttribute("disabled"));

        // The moment a row is typed the list becomes live again.
        cut.Find("input[type=text]").Change("BALCONY-LEFT");
        Assert.Contains("Admin.HallSeatLayouts.Validation.RowLabelLength", cut.Markup);
    }

    [Fact]
    public void The_remove_button_appears_only_when_a_layout_exists()
    {
        // B15 — nothing to remove on a hall that was never laid out.
        Arrange(hallCapacity: 100, Layout([], [], 100));
        var cut = RenderDeepLinked();

        Assert.DoesNotContain("Admin.HallSeatLayouts.Delete.Title", cut.Markup);
        Assert.DoesNotContain(">Admin.HallSeatLayouts.Delete<", cut.Markup);
    }

    [Fact]
    public void Removing_a_layout_asks_for_confirmation_first()
    {
        // B15 — the destructive call is gated by the must-decide SimfConfirm; clicking
        // Remove opens it rather than deleting straight away.
        Arrange(hallCapacity: 100, Layout(["A", "B"], [10, 10], 100));
        var cut = RenderDeepLinked();

        FindByText(cut, "Admin.HallSeatLayouts.Delete").Click();

        Assert.Contains("Admin.HallSeatLayouts.Delete.Title", cut.Markup);
        Assert.Contains("Admin.HallSeatLayouts.Delete.Confirm", cut.Markup);
    }

    [Fact]
    public void Confirming_the_removal_clears_the_grid_and_reports_success()
    {
        // B15 — the server answers with the now-empty snapshot; the editor mirrors it
        // so the preview, the capacity meter and the Remove button all clear at once.
        Arrange(hallCapacity: 100, Layout(["A", "B"], [10, 10], 100));
        JSInterop.Setup<ApiResult<HallSeatLayoutSnapshot>>(
                inv => inv.Identifier == "simfAccount.deleteJson")
            .SetResult(ApiResult<HallSeatLayoutSnapshot>.Ok(
                new HallSeatLayoutSnapshot(
                    HallId, [], 0, 0, 100, null, [])));
        var cut = RenderDeepLinked();

        FindByText(cut, "Admin.HallSeatLayouts.Delete").Click();
        FindConfirmButton(cut).Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.HallSeatLayouts.Deleted", cut.Markup));
        Assert.Equal(string.Empty, cut.Find("input[type=text]").GetAttribute("value"));
        Assert.DoesNotContain("Admin.HallSeatLayouts.Delete.Title", cut.Markup);
    }

    [Fact]
    public void A_refused_removal_shows_the_servers_reservation_count_message()
    {
        // B15 — SEAT_LAYOUT_HAS_RESERVATIONS names HOW MANY bookings block the
        // removal; the editor surfaces that server text, not a generic failure.
        Arrange(hallCapacity: 100, Layout(["A", "B"], [10, 10], 100));
        JSInterop.Setup<ApiResult<HallSeatLayoutSnapshot>>(
                inv => inv.Identifier == "simfAccount.deleteJson")
            .SetResult(ApiResult<HallSeatLayoutSnapshot>.Fail(new ApiError
            {
                Code = ErrorCodes.SeatLayoutHasReservations,
                Message = "Removing this layout would strand 3 active seat "
                    + "reservation(s). Release them before removing the layout.",
                MessageArabic = "ستؤدي إزالة هذا المخطط إلى إلغاء 3 حجز مقعد نشط. "
                    + "يرجى إلغاء هذه الحجوزات قبل إزالة المخطط.",
            }));
        var cut = RenderDeepLinked();

        FindByText(cut, "Admin.HallSeatLayouts.Delete").Click();
        FindConfirmButton(cut).Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("3 active seat reservation(s)", cut.Markup));
        // The layout is untouched — the rows are still there to release seats from.
        Assert.Equal("A,B", cut.Find("input[type=text]").GetAttribute("value"));
    }

    // -- Helpers --------------------------------------------------------------

    private static AngleSharp.Dom.IElement SaveButton(
        IRenderedComponent<HallSeatLayoutEditor> cut) =>
        FindByText(cut, "Admin.HallSeatLayouts.Save");

    // The localizer under test returns each key verbatim, so a button is found by
    // its key text. Buttons render their label inside a nested span, hence TextContent.
    private static AngleSharp.Dom.IElement FindByText(
        IRenderedComponent<HallSeatLayoutEditor> cut, string text) =>
        cut.FindAll("button")
            .First(button => button.TextContent.Trim() == text);

    // The confirmation dialog's own Remove button — the SECOND element carrying the
    // Delete label once the modal is open (the first is the one that opened it).
    private static AngleSharp.Dom.IElement FindConfirmButton(
        IRenderedComponent<HallSeatLayoutEditor> cut) =>
        cut.FindAll("button")
            .Where(button => button.TextContent.Trim() == "Admin.HallSeatLayouts.Delete")
            .Last();
}
