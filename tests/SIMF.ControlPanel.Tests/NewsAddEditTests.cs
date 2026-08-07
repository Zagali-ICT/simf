// D-353 rollout — behaviour tests for NewsAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs to the collection route, Edit PUTs to
// the row id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class NewsAddEditTests : CpComponentTestBase
{
    private static AdminNewsDetail Detail() => new(
        Guid.NewGuid(),
        "Forum opens", "افتتاح المنتدى",
        "Teaser", "مقتطف",
        "Body text", "نص المحتوى",
        "Announcements", "إعلانات",
        "news/cover.jpg",
        DateTime.UnixEpoch, 1,
        IsActive: true, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<NewsAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.News.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<NewsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.News.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminNewsDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminNewsDetail>.Ok(created));

        var cut = RenderComponent<NewsAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Fill the required fields (title / body / category, EN + AR) so the
        // client guard passes and the create request actually fires. The text
        // inputs (SimfTextField → InputText) raise their value on `change`; the
        // multi-line bodies (SimfTextarea) raise it on `input` — so drive each
        // with the matching event. Auto-refresh keeps the handles live across
        // the re-render each edit triggers.
        var inputs = cut.FindAll("input.simf-field__input", enableAutoRefresh: true);
        inputs[0].Change("Forum opens");      // Title (EN)
        inputs[1].Change("افتتاح المنتدى");    // Title (AR)
        inputs[2].Change("Announcements");    // Category (EN)
        inputs[3].Change("إعلانات");           // Category (AR)
        // Excerpt EN/AR are textareas[0]/[1]; Body EN/AR are the next two.
        var textareas = cut.FindAll("textarea.simf-field__input", enableAutoRefresh: true);
        textareas[2].Input("Body text");      // Body (EN)
        textareas[3].Input("نص المحتوى");     // Body (AR)

        cut.Find(".simf-form__actions .simf-button").Click();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/news", (string)posted.Arguments[0]!);
        Assert.IsType<CreateNewsRequest>(posted.Arguments[1]);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminNewsDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminNewsDetail>.Ok(row));

        var cut = RenderComponent<NewsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-form__actions .simf-button").Click();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/news/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<UpdateNewsRequest>(put.Arguments[1]);
    }
}
