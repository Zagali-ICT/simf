// D-353 rollout â€” behaviour tests for ContentBlockAddEdit (merged Add/Edit
// form). ContentBlocks upsert by Key, so both Add and Edit PUT the same
// /account/api/admin/content-blocks route; the only difference is that the Key
// field is editable on Add and read-only (disabled) on Edit. Pins both branches
// and the upsert body.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ContentBlockAddEditTests : CpComponentTestBase
{
    private static AdminContentBlockSummary Summary() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "home.welcome.title", "Welcome", "Ø£Ù‡Ù„Ø§Ù‹",
        IsActive: true, DateTimeOffset.UnixEpoch, Guid.Empty);

    [Fact]
    public void Add_mode_leaves_the_key_field_editable()
    {
        var cut = RenderComponent<ContentBlockAddEdit>(p => p.Add(x => x.IsEdit, false));
        var key = cut.FindAll("input.simf-field__input")[0];
        Assert.False(key.HasAttribute("disabled"));
    }

    [Fact]
    public void Edit_mode_disables_the_key_field()
    {
        var cut = RenderComponent<ContentBlockAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Summary()));
        var key = cut.FindAll("input.simf-field__input")[0];
        Assert.True(key.HasAttribute("disabled"));
    }

    [Fact]
    public void Add_mode_puts_an_upsert_request_with_the_typed_key()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var saved = Summary();
        var handler = JSInterop.Setup<ApiResult<AdminContentBlockSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminContentBlockSummary>.Ok(saved));

        var cut = RenderComponent<ContentBlockAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("home.welcome.title");  // Key
        cut.FindAll("input.simf-field__input")[1].Change("Welcome");            // Content (EN)
        cut.FindAll("input.simf-field__input")[2].Change("Ø£Ù‡Ù„Ø§Ù‹"); // Content (AR)
        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/content-blocks", (string)put.Arguments[0]!);
        var body = (UpsertContentBlockRequest)put.Arguments[1]!;
        Assert.Equal("home.welcome.title", body.Key);
        Assert.Equal("Welcome", body.Content);
    }

    [Fact]
    public void Edit_mode_puts_an_upsert_request_keeping_the_existing_key()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Summary();
        var handler = JSInterop.Setup<ApiResult<AdminContentBlockSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminContentBlockSummary>.Ok(row));

        var cut = RenderComponent<ContentBlockAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/content-blocks", (string)put.Arguments[0]!);
        var body = (UpsertContentBlockRequest)put.Arguments[1]!;
        Assert.Equal(row.Key, body.Key);
    }
}
