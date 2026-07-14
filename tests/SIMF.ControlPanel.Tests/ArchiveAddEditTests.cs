// D-353 rollout â€” behaviour tests for ArchiveAddEdit (merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs a CreateArchiveEditionRequest, Edit PUTs
// an UpdateArchiveEditionRequest to the row id, and the Active checkbox only
// appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Archive;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class ArchiveAddEditTests : CpComponentTestBase
{
    private static AdminArchiveEditionSummary Summary() => new(
        Guid.NewGuid(), 2024, "SIMF 2024", "Ø³ÙŠÙ…Ù 2024",
        "Summary EN", "Summary AR", 1200, 30, 18,
        "/media/archive/2024.jpg", IsActive: true, DateTimeOffset.UnixEpoch,
        false, // HasCover (D-357)
        "Riyadh", "Ø§Ù„Ø±ÙŠØ§Ø¶", "Mar 2024", "Ù…Ø§Ø±Ø³ 2024");

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<ArchiveAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.Archive.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<ArchiveAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Summary()));
        Assert.Contains("Admin.Archive.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request_with_the_titles()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminArchiveEditionDetail(
            Guid.NewGuid(), 2025, "SIMF 2025", "Ø³ÙŠÙ…Ù 2025",
            null, null, 0, 0, 0, null, true, DateTimeOffset.UnixEpoch, null);
        var handler = JSInterop.Setup<ApiResult<AdminArchiveEditionDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminArchiveEditionDetail>.Ok(created));

        var cut = RenderComponent<ArchiveAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Field order: [0] Year (native), [1] TitleEn, [2] TitleAr (SimfTextField).
        cut.FindAll("input.simf-field__input")[1].Change("SIMF 2025");
        cut.FindAll("input.simf-field__input")[2].Change("Ø³ÙŠÙ…Ù 2025");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/archive", (string)posted.Arguments[0]!);
        var body = (CreateArchiveEditionRequest)posted.Arguments[1]!;
        Assert.Equal("SIMF 2025", body.TitleEn);
        Assert.Equal("Ø³ÙŠÙ…Ù 2025", body.TitleAr);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Summary();
        var detail = new AdminArchiveEditionDetail(
            row.Id, row.Year, row.TitleEn, row.TitleAr, row.SummaryEn, row.SummaryAr,
            row.Attendees, row.Sessions, row.Speakers, row.CoverImageRelativePath,
            row.IsActive, row.CreatedAt, null,
            row.LocationEn, row.LocationAr, row.DateLabelEn, row.DateLabelAr);
        var handler = JSInterop.Setup<ApiResult<AdminArchiveEditionDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminArchiveEditionDetail>.Ok(detail));

        var cut = RenderComponent<ArchiveAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/archive/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<UpdateArchiveEditionRequest>(put.Arguments[1]);
    }
}
