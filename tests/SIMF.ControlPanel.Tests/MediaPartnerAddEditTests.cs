// D-353 rollout â€” behaviour tests for MediaPartnerAddEdit (merged Add/Edit
// form). Pins the IsEdit branch: Create POSTs a positional create request, Edit
// PUTs to the row id, and the Active checkbox only appears in Edit mode.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class MediaPartnerAddEditTests : CpComponentTestBase
{
    private static AdminMediaPartnerDetail Detail() => new(
        Guid.NewGuid(), "Maritime Times", "Ø£ÙˆÙ‚Ø§Øª Ø¨Ø­Ø±ÙŠØ©",
        "media/partners/mt.png", "https://example.com", 3,
        IsActive: true, DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<MediaPartnerAddEdit>(p => p.Add(x => x.IsEdit, false));
        Assert.DoesNotContain("Admin.MediaPartners.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var cut = RenderComponent<MediaPartnerAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail()));
        Assert.Contains("Admin.MediaPartners.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminMediaPartnerDetail(
            Guid.NewGuid(), "New Partner", "Ø´Ø±ÙŠÙƒ Ø¬Ø¯ÙŠØ¯", null, null, 0,
            true, DateTimeOffset.UnixEpoch, null, null);
        var handler = JSInterop.Setup<ApiResult<AdminMediaPartnerDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminMediaPartnerDetail>.Ok(created));

        var cut = RenderComponent<MediaPartnerAddEdit>(p => p.Add(x => x.IsEdit, false));

        // Re-query after each Change â€” @bind re-renders and invalidates handles.
        cut.FindAll("input.simf-field__input")[0].Change("New Partner");   // Name (EN)
        cut.FindAll("input.simf-field__input")[1].Change("Ø´Ø±ÙŠÙƒ Ø¬Ø¯ÙŠØ¯");      // Name (AR)
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/media-partners", (string)posted.Arguments[0]!);
        var body = (AdminCreateMediaPartnerRequest)posted.Arguments[1]!;
        Assert.Equal("New Partner", body.Name);
        Assert.Equal("Ø´Ø±ÙŠÙƒ Ø¬Ø¯ÙŠØ¯", body.NameArabic);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminMediaPartnerDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminMediaPartnerDetail>.Ok(row));

        var cut = RenderComponent<MediaPartnerAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/media-partners/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateMediaPartnerRequest>(put.Arguments[1]);
    }
}
