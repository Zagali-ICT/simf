// A40 — the seat-layout editor had NO row action on the Halls grid, so an admin
// looking at a hall had no way to reach its seat map from there. These pin the
// quiet-icon row action: it renders per row, it deep-links to that hall, and it
// is hidden from an admin who does not hold SeatLayouts.View.
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class HallsListSeatLayoutActionTests : CpComponentTestBase
{
    private static readonly Guid HallId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AdminHallSummary Hall() =>
        new(HallId, "H-1", "Main Hall", "القاعة الرئيسية", 500, "G",
            IsActive: true, DateTimeOffset.UtcNow);

    private void Arrange(params string[] grantedPermissions)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        Authorization.SetPolicies(
            grantedPermissions.Select(PermissionCatalog.PolicyFor).ToArray());

        var halls = new[] { Hall() };
        JSInterop.Setup<ApiResult<GridPage<AdminHallSummary>>>(
                inv => inv.Identifier == "simfAccount.postJson")
            .SetResult(ApiResult<GridPage<AdminHallSummary>>.Ok(
                GridPage<AdminHallSummary>.Of(halls, halls.Length, new GridQuery())));
    }

    [Fact]
    public void The_grid_offers_a_seat_layout_row_action()
    {
        Arrange(PermissionCatalog.Halls.View, PermissionCatalog.SeatLayouts.View);

        var cut = RenderComponent<HallsList>();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(SeatLayoutButtons(cut)));
    }

    [Fact]
    public void The_seat_layout_row_action_deep_links_to_that_hall()
    {
        // A40 — the editor reads ?hallId=, so the admin lands on the hall's own grid
        // instead of a blank picker they have to re-find the hall in.
        Arrange(PermissionCatalog.Halls.View, PermissionCatalog.SeatLayouts.View);
        var nav = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<HallsList>();
        cut.WaitForAssertion(() => Assert.NotEmpty(SeatLayoutButtons(cut)));
        SeatLayoutButtons(cut).First().Click();

        Assert.EndsWith($"/admin/halls/seat-layouts?hallId={HallId}", nav.Uri);
    }

    [Fact]
    public void The_seat_layout_row_action_is_hidden_without_the_permission()
    {
        // The action is wrapped in AuthorizedAction, so an admin who cannot view seat
        // layouts is not offered a button that would only 403.
        Arrange(PermissionCatalog.Halls.View);

        var cut = RenderComponent<HallsList>();

        cut.WaitForAssertion(() => Assert.Contains("Main Hall", cut.Markup));
        Assert.Empty(SeatLayoutButtons(cut));
    }

    // The localizer under test returns each key verbatim, so the row action is found
    // by the title attribute the quiet icon button renders.
    private static IReadOnlyList<AngleSharp.Dom.IElement> SeatLayoutButtons(
        IRenderedComponent<HallsList> cut) =>
        cut.FindAll("button[title='Admin.Halls.Action.SeatLayout']");
}
