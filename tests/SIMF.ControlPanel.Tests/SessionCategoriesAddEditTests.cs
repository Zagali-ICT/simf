// D-353 rollout â€” behaviour tests for SessionCategoriesAddEdit (merged
// Add/Edit form). Pins the IsEdit branch: Create POSTs, Edit PUTs to the row
// id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionCategoriesAddEditTests : CpComponentTestBase
{
    private static AdminSessionCategoryDetail Detail() => new(
        Guid.NewGuid(), "Keynotes", "ÙƒÙ„Ù…Ø§Øª Ø±Ø¦ÙŠØ³ÙŠØ©", 1,
        IsActive: true, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<SessionCategoriesAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.SessionCategories.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<SessionCategoriesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.SessionCategories.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminSessionCategoryDetail(
            Guid.NewGuid(), "Panels", "Ø­Ù„Ù‚Ø§Øª Ù†Ù‚Ø§Ø´", 0, true, DateTime.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminSessionCategoryDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminSessionCategoryDetail>.Ok(created));

        var cut = RenderComponent<SessionCategoriesAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("Panels");        // Name (English)
        cut.FindAll("input.simf-field__input")[1].Change("Ø­Ù„Ù‚Ø§Øª Ù†Ù‚Ø§Ø´");     // Name (Arabic)
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/session-categories", (string)posted.Arguments[0]!);
        var body = (AdminCreateSessionCategoryRequest)posted.Arguments[1]!;
        Assert.Equal("Panels", body.Name);
        Assert.Equal("Ø­Ù„Ù‚Ø§Øª Ù†Ù‚Ø§Ø´", body.NameArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminSessionCategoryDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminSessionCategoryDetail>.Ok(row));

        var cut = RenderComponent<SessionCategoriesAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/session-categories/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateSessionCategoryRequest>(put.Arguments[1]);
    }
}
