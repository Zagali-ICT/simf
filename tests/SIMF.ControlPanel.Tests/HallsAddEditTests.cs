// D-353 rollout â€” behaviour tests for HallsAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs a create request, Edit PUTs to the row
// id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class HallsAddEditTests : CpComponentTestBase
{
    private static AdminHallDetail Detail() => new(
        Guid.NewGuid(), "HALL-A", "Main Hall", "Ø§Ù„Ù‚Ø§Ø¹Ø© Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠØ©", 250, "Ground",
        FacilityNotes: "Projector + PA", IsActive: true,
        DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<HallsAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Halls.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<HallsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Halls.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminHallDetail(
            Guid.NewGuid(), "HALL-B", "Side Hall", "Ø§Ù„Ù‚Ø§Ø¹Ø© Ø§Ù„Ø¬Ø§Ù†Ø¨ÙŠØ©", 80, null,
            null, true, DateTime.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminHallDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminHallDetail>.Ok(created));

        var cut = RenderComponent<HallsAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("HALL-B");        // Code
        cut.FindAll("input.simf-field__input")[1].Change("Side Hall");     // Name
        cut.FindAll("input.simf-field__input")[2].Change("Ø§Ù„Ù‚Ø§Ø¹Ø© Ø§Ù„Ø¬Ø§Ù†Ø¨ÙŠØ©"); // NameArabic
        cut.FindAll("input.simf-field__input")[3].Change("80");            // Capacity
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/halls", (string)posted.Arguments[0]!);
        var body = (AdminCreateHallRequest)posted.Arguments[1]!;
        Assert.Equal("HALL-B", body.Code);
        Assert.Equal("Side Hall", body.Name);
        Assert.Equal(80, body.Capacity);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminHallDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminHallDetail>.Ok(row));

        var cut = RenderComponent<HallsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/halls/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateHallRequest>(put.Arguments[1]);
    }
}
