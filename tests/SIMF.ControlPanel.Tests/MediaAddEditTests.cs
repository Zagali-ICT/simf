// D-353 rollout â€” behaviour tests for MediaAddEdit (merged Add/Edit form for the
// Media gallery). Pins the IsEdit branch (Create POSTs, Edit PUTs to the row id),
// the Video-URL guard, and the out-of-row image-upload flow: creating an Image
// keeps the form open in Edit mode (so the bitmap can be attached) rather than
// closing the shell, while creating a Video raises OnSuccess.
using Bunit;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class MediaAddEditTests : CpComponentTestBase
{
    private static AdminMediaDetail Detail(MediaKind kind = MediaKind.Video) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        kind,
        "Opening ceremony", "Ø­ÙÙ„ Ø§Ù„Ø§ÙØªØªØ§Ø­",
        "Day 1", "Ø§Ù„ÙŠÙˆÙ… Ø§Ù„Ø£ÙˆÙ„",
        HasImage: false, HasThumbnail: false,
        Url: kind == MediaKind.Video ? "https://youtu.be/abc" : null,
        DisplayOrder: 3, IsActive: true,
        CreatedAt: DateTimeOffset.UnixEpoch, UpdatedAt: null);

    [Fact]
    public void Add_mode_shows_the_save_first_hint_for_an_image()
    {
        var cut = RenderComponent<MediaAddEdit>(p => p.Add(x => x.IsEdit, false));
        // Default kind is Image; the upload control is hidden until the row exists.
        Assert.Contains("Admin.Media.Image.SaveFirst", cut.Markup);
        Assert.Empty(cut.FindAll("#media-image-input"));
    }

    [Fact]
    public void Edit_mode_for_an_image_shows_the_upload_control()
    {
        var cut = RenderComponent<MediaAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail(MediaKind.Image)));
        Assert.NotEmpty(cut.FindAll("#media-image-input"));
    }

    [Fact]
    public void Video_without_url_does_not_post()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var handler = JSInterop.Setup<ApiResult<AdminMediaDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminMediaDetail>.Ok(Detail()));

        var cut = RenderComponent<MediaAddEdit>(p => p.Add(x => x.IsEdit, false));
        // Switch to Video kind but leave the URL blank, then Save.
        cut.Find("select.simf-field__input").Change(nameof(MediaKind.Video));
        cut.Find(".simf-form__actions .simf-button").Click();

        Assert.Empty(handler.Invocations);
        Assert.Contains("Admin.Media.VideoUrlRequired", cut.Markup);
    }

    [Fact]
    public void Adding_a_video_posts_a_create_request_and_raises_success()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminMediaDetail>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminMediaDetail>.Ok(created));

        AdminMediaDetail? raised = null;
        var cut = RenderComponent<MediaAddEdit>(p => p
            .Add(x => x.IsEdit, false)
            .Add(x => x.OnSuccess, (AdminMediaDetail d) => raised = d));

        cut.Find("select.simf-field__input").Change(nameof(MediaKind.Video));
        // The Video URL field is the LAST text field (after Title/TitleArabic/
        // Album/AlbumArabic), which only appears once kind == Video.
        var textInputs = cut.FindAll("input.simf-field__input")
            .Where(i => !i.HasAttribute("type") || i.GetAttribute("type") == "text").ToList();
        textInputs[textInputs.Count - 1].Change("https://youtu.be/abc");
        cut.Find(".simf-form__actions .simf-button").Click();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/media", (string)posted.Arguments[0]!);
        var body = (AdminCreateMediaRequest)posted.Arguments[1]!;
        Assert.Equal(MediaKind.Video, body.Kind);
        Assert.Equal("https://youtu.be/abc", body.Url);
        Assert.NotNull(raised);
    }

    [Fact]
    public void Adding_an_image_stays_open_in_edit_mode_for_upload()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = Detail(MediaKind.Image);
        JSInterop.Setup<ApiResult<AdminMediaDetail>>("simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminMediaDetail>.Ok(created));

        var successRaised = false;
        var cut = RenderComponent<MediaAddEdit>(p => p
            .Add(x => x.IsEdit, false)
            .Add(x => x.OnSuccess, (AdminMediaDetail _) => successRaised = true));

        // Default kind is Image; Save the metadata row.
        cut.Find(".simf-form__actions .simf-button").Click();

        // The shell stays open (no OnSuccess) and the upload control now shows.
        Assert.False(successRaised);
        Assert.NotEmpty(cut.FindAll("#media-image-input"));
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail();
        var handler = JSInterop.Setup<ApiResult<AdminMediaDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminMediaDetail>.Ok(row));

        var cut = RenderComponent<MediaAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        cut.Find(".simf-form__actions .simf-button").Click();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/media/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateMediaRequest>(put.Arguments[1]);
    }
}
