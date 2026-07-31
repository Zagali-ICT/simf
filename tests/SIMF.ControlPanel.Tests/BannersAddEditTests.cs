// D-353 rollout â€” behaviour tests for BannersAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs a CreateBannerRequest, Edit PUTs an
// UpdateBannerRequest to the row id, and the Active checkbox only appears in
// Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class BannersAddEditTests : CpComponentTestBase
{
    private static AdminBannerDetail Detail() => new(
        Guid.NewGuid(), "Welcome", "Ø£Ù‡Ù„Ø§Ù‹", "Body", "Ø§Ù„Ù†Øµ",
        "https://cdn.simf.test/banner.png", "https://simf.test/news",
        DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1),
        DisplayOrder: 3, IsActive: true, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<BannersAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Banners.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<BannersAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.Banners.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminBannerDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminBannerDetail>.Ok(created));

        var cut = RenderComponent<BannersAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("Welcome");      // Title (EN)
        cut.FindAll("input.simf-field__input")[1].Change("Ø£Ù‡Ù„Ø§Ù‹");         // Title (AR)
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/banners", (string)posted.Arguments[0]!);
        var body = (CreateBannerRequest)posted.Arguments[1]!;
        Assert.Equal("Welcome", body.Title);
        Assert.Equal("Ø£Ù‡Ù„Ø§Ù‹", body.TitleArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminBannerDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminBannerDetail>.Ok(row));

        var cut = RenderComponent<BannersAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/banners/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<UpdateBannerRequest>(put.Arguments[1]);
    }
}
