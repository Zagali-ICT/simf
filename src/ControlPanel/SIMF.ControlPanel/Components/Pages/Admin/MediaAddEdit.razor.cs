using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Logs;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Media;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FormModel _model = new();
    private bool _busy;
    private string? _error;

    // _isEditing tracks Edit mode locally: it starts from IsEdit but flips to
    // true after a successful Image create so the upload control appears
    // in-place (the shell stays open). _editingId is the row the PUT/upload
    // targets â€” Initial!.Id in Edit, or the just-created id after an Image POST.
    private bool _isEditing;
    private Guid _editingId;

    // Image-upload state (Image-kind items only). _hasImage tracks whether
    // bytes already exist server-side; _imageName is the picked file name.
    private bool _hasImage;
    private string? _imageName;
    private bool _uploadingImage;

    protected override void OnInitialized()
    {
        _isEditing = IsEdit;
        if (Initial is not null)
        {
            _editingId = Initial.Id;
            _hasImage = Initial.HasImage;
            _model.Kind = Initial.Kind;
            _model.Title = Initial.Title ?? string.Empty;
            _model.TitleArabic = Initial.TitleArabic ?? string.Empty;
            _model.Album = Initial.Album ?? string.Empty;
            _model.AlbumArabic = Initial.AlbumArabic ?? string.Empty;
            _model.Url = Initial.Url ?? string.Empty;
            _model.DisplayOrder = Initial.DisplayOrder;
            _model.IsActive = Initial.IsActive;
        }
    }

    private async Task SaveAsync()
    {
        if (_busy) return;
        // Video items must carry an external playback URL; an Image item's
        // bitmap is attached separately so it has no required text here.
        if (_model.Kind == MediaKind.Video && string.IsNullOrWhiteSpace(_model.Url))
        {
            _error = L["Admin.Media.VideoUrlRequired"];
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            if (_isEditing)
            {
                var env = await JS.InvokeAsync<ApiResult<AdminMediaDetail>>(
                    "simfAccount.putJson",
                    $"/account/api/admin/media/{_editingId}",
                    new AdminUpdateMediaRequest
                    {
                        Kind = _model.Kind,
                        Title = NullIfBlank(_model.Title),
                        TitleArabic = NullIfBlank(_model.TitleArabic),
                        Album = NullIfBlank(_model.Album),
                        AlbumArabic = NullIfBlank(_model.AlbumArabic),
                        Url = NullIfBlank(_model.Url),
                        DisplayOrder = _model.DisplayOrder,
                        IsActive = _model.IsActive,
                    });
                if (env is { Success: true, Data: not null })
                {
                    await OnSuccess.InvokeAsync(env.Data);
                }
                else
                {
                    _error = env?.Error?.MessageForCurrentCulture()
                        ?? L["Admin.Media.LoadFailed"];
                }
            }
            else
            {
                var env = await JS.InvokeAsync<ApiResult<AdminMediaDetail>>(
                    "simfAccount.postJson",
                    "/account/api/admin/media",
                    new AdminCreateMediaRequest
                    {
                        Kind = _model.Kind,
                        Title = NullIfBlank(_model.Title),
                        TitleArabic = NullIfBlank(_model.TitleArabic),
                        Album = NullIfBlank(_model.Album),
                        AlbumArabic = NullIfBlank(_model.AlbumArabic),
                        Url = NullIfBlank(_model.Url),
                        DisplayOrder = _model.DisplayOrder,
                    });
                if (env is { Success: true, Data: not null })
                {
                    // Image items have no bytes yet â€” keep the form open in
                    // Edit mode so the admin can attach the bitmap now. Video
                    // items are complete on create, so close the shell.
                    if (_model.Kind == MediaKind.Image)
                    {
                        var d = env.Data;
                        _isEditing = true;
                        _editingId = d.Id;
                        _hasImage = d.HasImage;
                        _imageName = null;
                        _model.DisplayOrder = d.DisplayOrder;
                        _model.IsActive = d.IsActive;
                        _error = null;
                    }
                    else
                    {
                        await OnSuccess.InvokeAsync(env.Data);
                    }
                }
                else
                {
                    _error = env?.Error?.MessageForCurrentCulture()
                        ?? L["Admin.Media.LoadFailed"];
                }
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Media.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private async Task OnImageSelected(ChangeEventArgs _)
    {
        // Capture the picked file name so the Upload button can enable;
        // the bytes ride the hidden <input> via simfAccount.uploadFile.
        var name = await JS.InvokeAsync<string?>(
            "eval", "document.getElementById('media-image-input')?.files?.[0]?.name ?? null");
        _imageName = name;
    }

    private async Task UploadImageAsync()
    {
        if (_busy || _uploadingImage || string.IsNullOrEmpty(_imageName)) return;
        _uploadingImage = true;
        _error = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminMediaDetail>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/media/{_editingId}/image",
                "media-image-input");
            if (env is { Success: true, Data: not null })
            {
                _hasImage = env.Data.HasImage;
                _imageName = null;
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Media.Image.UploadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Media.Image.UploadFailed"];
        }
        finally { _uploadingImage = false; }
    }

    private void OnKindChanged(ChangeEventArgs e)
    {
        _model.Kind = Enum.TryParse<MediaKind>(e.Value?.ToString(), out var k)
            ? k : MediaKind.Image;
    }

    private void OnDisplayOrderChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.DisplayOrder = n;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FormModel
    {
        public MediaKind Kind { get; set; } = MediaKind.Image;
        public string Title { get; set; } = string.Empty;
        public string TitleArabic { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumArabic { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
