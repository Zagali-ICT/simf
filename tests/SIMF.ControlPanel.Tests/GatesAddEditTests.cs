// D-353 rollout â€” behaviour tests for GatesAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs, Edit PUTs to the row id, and the Active
// checkbox only appears in Edit mode. The form also loads profile-types + admin
// candidate-operator lists in OnInitializedAsync; those calls return a different
// generic (ApiResult<GridPage<...>>) so they don't collide with the
// ApiResult<AdminGateDetail> create/update handler under loose JSInterop.
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class GatesAddEditTests : CpComponentTestBase
{
    private static AdminGateDetail Detail() => new(
        Guid.NewGuid(), "GATE-1", "North Gate", "Ø§Ù„Ø¨ÙˆØ§Ø¨Ø© Ø§Ù„Ø´Ù…Ø§Ù„ÙŠØ©",
        Description: "Main entrance", DescriptionArabic: "Ø§Ù„Ù…Ø¯Ø®Ù„ Ø§Ù„Ø±Ø¦ÙŠØ³ÙŠ",
        DirectionMode.Both, IsActive: true,
        AllowedProfileTypeIds: Array.Empty<Guid>(),
        AssignedOperatorUserIds: Array.Empty<Guid>(),
        DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<GatesAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Gates.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<GatesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Gates.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminGateDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminGateDetail>.Ok(created));

        var cut = RenderComponent<GatesAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("GATE-X");      // code
        cut.FindAll("input.simf-field__input")[1].Change("Test Gate");   // name
        cut.FindAll("input.simf-field__input")[2].Change("Ø¨ÙˆØ§Ø¨Ø© Ø§Ø®ØªØ¨Ø§Ø±"); // name arabic
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/gates", (string)posted.Arguments[0]!);
        var body = (AdminCreateGateRequest)posted.Arguments[1]!;
        Assert.Equal("GATE-X", body.Code);
        Assert.Equal("Test Gate", body.Name);
        Assert.Equal("Ø¨ÙˆØ§Ø¨Ø© Ø§Ø®ØªØ¨Ø§Ø±", body.NameArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminGateDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminGateDetail>.Ok(row));

        var cut = RenderComponent<GatesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/gates/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateGateRequest>(put.Arguments[1]);
    }
}
