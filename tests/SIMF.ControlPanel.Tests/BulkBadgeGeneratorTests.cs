// Behaviour tests for the #10 bulk badge generator (BulkBadgeGenerator),
// redesigned 2026-07-22 into a batch-builder: pick a profile type + count and
// press Add to build a batch list, then Generate → confirm popup posts the batch
// to /account/api/admin/visitors/bulk-generate. Pins the builder logic (add /
// merge / total) and the request contract.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class BulkBadgeGeneratorTests : CpComponentTestBase
{
    private static readonly Guid VipId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NormalId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static AdminProfileTypeSummary Type(Guid id, string name) =>
        new(id, name, name, "#244A77", "Visitor", "None",
            IsActive: true, IsVisitor: true, IsAppRegisterable: true);

    private IRenderedComponent<BulkBadgeGenerator> RenderWithTypes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary> { Type(VipId, "VIP"), Type(NormalId, "Normal") }));
        return RenderComponent<BulkBadgeGenerator>();
    }

    private static void AddRow(IRenderedComponent<BulkBadgeGenerator> cut, Guid typeId, string count)
    {
        cut.Find(".simf-bulk-builder__add select").Change(typeId.ToString());
        // The count SimfTextField binds on oninput (not onchange).
        cut.Find(".simf-bulk-builder__add input").Input(count);
        cut.Find(".simf-bulk-builder__add-action button").Click();
    }

    [Fact]
    public void Shows_the_add_row_and_the_empty_state()
    {
        var cut = RenderWithTypes();
        Assert.NotEmpty(cut.FindAll(".simf-bulk-builder__add select"));
        Assert.Contains("Admin.Delegates.Bulk.Empty", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-bulk-builder__row"));
    }

    [Fact]
    public void Add_appends_a_batch_row_with_the_count()
    {
        var cut = RenderWithTypes();
        AddRow(cut, VipId, "5");

        var rows = cut.FindAll(".simf-bulk-builder__row");
        Assert.Single(rows);
        Assert.Contains("VIP", rows[0].TextContent);
        Assert.Contains("× 5", rows[0].TextContent);
        Assert.DoesNotContain("Admin.Delegates.Bulk.Empty", cut.Markup);
    }

    [Fact]
    public void Adding_the_same_type_twice_merges_into_one_row()
    {
        var cut = RenderWithTypes();
        AddRow(cut, VipId, "5");
        AddRow(cut, VipId, "3");

        var rows = cut.FindAll(".simf-bulk-builder__row");
        Assert.Single(rows);
        Assert.Contains("× 8", rows[0].TextContent);
    }

    [Fact]
    public void Add_with_no_selection_shows_the_pick_type_message()
    {
        var cut = RenderWithTypes();
        // Click Add without choosing a type / count.
        cut.Find(".simf-bulk-builder__add-action button").Click();
        Assert.Contains("Admin.Delegates.Bulk.PickType", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-bulk-builder__row"));
    }

    [Fact]
    public void Generate_posts_the_built_batch_to_the_bulk_endpoint()
    {
        var cut = RenderWithTypes();
        var handler = JSInterop.Setup<ApiResult<AdminBulkGenerateBadgesResponse>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminBulkGenerateBadgesResponse>.Ok(
                new AdminBulkGenerateBadgesResponse(5)));

        AddRow(cut, VipId, "5");
        // Open the confirm popup, then confirm.
        cut.Find(".simf-form__actions button").Click();
        cut.FindAll(".simf-modal__footer button").Last().Click();

        var post = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/visitors/bulk-generate", (string)post.Arguments[0]!);
        var body = (AdminBulkGenerateBadgesRequest)post.Arguments[1]!;
        Assert.Single(body.Batches);
        Assert.Equal(VipId, body.Batches[0].ProfileTypeId);
        Assert.Equal(5, body.Batches[0].Count);
    }
}
