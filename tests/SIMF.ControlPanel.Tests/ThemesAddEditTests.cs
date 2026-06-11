// D-353 rollout â€” behaviour tests for ThemesAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs a create request, Edit PUTs to the row
// id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ThemesAddEditTests : CpComponentTestBase
{
    private static AdminThemeDetail Detail() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "DEF", "Defence", "Ø§Ù„Ø¯ÙØ§Ø¹", "Defence theme", "Ù…Ø­ÙˆØ± Ø§Ù„Ø¯ÙØ§Ø¹",
        1, "#244A77", IsActive: true, DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<ThemesAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Themes.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<ThemesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Themes.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminThemeDetail(
            Guid.NewGuid(), "TECH", "Technology", "Ø§Ù„ØªÙ‚Ù†ÙŠØ©", null, null,
            0, "#244A77", true, DateTimeOffset.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminThemeDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminThemeDetail>.Ok(created));

        var cut = RenderComponent<ThemesAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("TECH");         // code
        cut.FindAll("input.simf-field__input")[1].Change("Technology");   // name (EN)
        cut.FindAll("input.simf-field__input")[2].Change("Ø§Ù„ØªÙ‚Ù†ÙŠØ©");      // name (AR)
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/themes", (string)posted.Arguments[0]!);
        var body = (AdminCreateThemeRequest)posted.Arguments[1]!;
        Assert.Equal("TECH", body.Code);
        Assert.Equal("Technology", body.Name);
        Assert.Equal("Ø§Ù„ØªÙ‚Ù†ÙŠØ©", body.NameArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminThemeDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminThemeDetail>.Ok(row));

        var cut = RenderComponent<ThemesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/themes/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateThemeRequest>(put.Arguments[1]);
    }
}
