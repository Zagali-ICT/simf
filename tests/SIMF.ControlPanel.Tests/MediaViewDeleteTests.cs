// D-353 rollout â€” behaviour tests for MediaViewDelete (merged View/Delete form
// for the Media gallery). View shows the read-only details only; Delete mode
// gates the soft-delete behind SimfConfirm (no DELETE until confirmed) and
// targets the existing /admin/media/{id} endpoint.
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class MediaViewDeleteTests : CpComponentTestBase
{
    private static AdminMediaDetail Detail(MediaKind kind = MediaKind.Image) => new(
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        kind,
        "Opening ceremony", "Ø­ÙÙ„ Ø§Ù„Ø§ÙØªØªØ§Ø­",
        "Day 1", "Ø§Ù„ÙŠÙˆÙ… Ø§Ù„Ø£ÙˆÙ„",
        HasImage: true, HasThumbnail: false,
        Url: kind == MediaKind.Video ? "https://youtu.be/abc" : null,
        DisplayOrder: 3, IsActive: true,
        CreatedAt: DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void View_mode_shows_details_and_no_delete_button()
    {
        var cut = RenderComponent<MediaViewDelete>(p => p
            .Add(x => x.IsDelete, false)
            .Add(x => x.Initial, Detail()));

        Assert.Contains("Opening ceremony", cut.Markup);
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
        var cut = RenderComponent<MediaViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-button--danger").Click();
        Assert.NotEmpty(cut.FindAll(".simf-modal"));      // SimfConfirm opened
        Assert.Empty(deleteHandler.Invocations);          // nothing deleted yet

        cut.Find(".simf-modal__footer .simf-button--danger").Click();
        var del = deleteHandler.Invocations.Single();
        Assert.Equal($"/account/api/admin/media/{row.Id}", (string)del.Arguments[0]!);
    }

    [Fact]
    public void Cancelling_the_confirmation_fires_no_delete()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var deleteHandler = JSInterop.Setup<ApiResult<bool>>(
            "simfAccount.deleteJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));

        var cut = RenderComponent<MediaViewDelete>(p => p
            .Add(x => x.IsDelete, true)
            .Add(x => x.Initial, Detail()));

        cut.Find(".simf-button--danger").Click();
        cut.Find(".simf-modal__footer .simf-button--secondary").Click();

        Assert.Empty(deleteHandler.Invocations);
    }
}
