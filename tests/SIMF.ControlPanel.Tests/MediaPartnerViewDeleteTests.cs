// D-353 rollout â€” behaviour tests for MediaPartnerViewDelete (merged View/Delete
// form). View shows details only; Delete mode gates the soft-delete behind
// SimfConfirm (no DELETE until confirmed).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class MediaPartnerViewDeleteTests : CpComponentTestBase
{
    public MediaPartnerViewDeleteTests()
    {
        // D-833 — the confirm button is gated on the code the endpoint behind
        // it needs; this test drives that button, so the identity holds it.
        Grant(PermissionCatalog.MediaPartners.Delete);
    }

    private static AdminMediaPartnerDetail Detail() => new(
        Guid.NewGuid(), "Maritime Times", "Ø£ÙˆÙ‚Ø§Øª Ø¨Ø­Ø±ÙŠØ©",
        "media/partners/mt.png", "https://example.com", 3,
        IsActive: true, DateTime.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var cut = RenderComponent<MediaPartnerViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Detail()));

        Assert.Contains("Maritime Times", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-button--danger"));
        Assert.Empty(cut.FindAll(".simf-modal"));
    }

    [Fact]
    public void Delete_mode_gates_the_call_behind_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var row = Detail();
        var cut = RenderComponent<MediaPartnerViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/media-partners/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<MediaPartnerViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Detail()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }
}
