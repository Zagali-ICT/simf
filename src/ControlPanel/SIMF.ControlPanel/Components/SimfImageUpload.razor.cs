using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;

namespace SIMF.ControlPanel.Components;

public partial class SimfImageUpload
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    private enum Mode { Upload, Link }
    private Mode _mode = Mode.Upload;
    private readonly string _inputId = $"asset-file-{Guid.NewGuid():N}";
    private string _uploadKind = "Image";
    private string _linkKind = "Image";
    private string _url = string.Empty;
    private bool _busy;
    private string? _error;
    private string? _success;
    private int _version;

    /// <summary>The asset category enum name (e.g. "SpeakerPhoto").</summary>
    [Parameter, EditorRequired] public string Category { get; set; } = string.Empty;

    /// <summary>The owning entity's id. <c>Guid.Empty</c> shows the "save first" hint.</summary>
    [Parameter, EditorRequired] public Guid OwnerId { get; set; }

    [Parameter] public string? Alt { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    // Optional per-call label overrides; each defaults to the shared Admin.Asset.* resource.
    [Parameter] public string? SaveFirstLabel { get; set; }
    [Parameter] public string? UploadTabLabel { get; set; }
    [Parameter] public string? LinkTabLabel { get; set; }
    [Parameter] public string? FileLabel { get; set; }
    [Parameter] public string? UploadButtonLabel { get; set; }
    [Parameter] public string? UrlLabel { get; set; }
    [Parameter] public string? KindLabel { get; set; }
    [Parameter] public string? KindImageLabel { get; set; }
    [Parameter] public string? KindVideoLabel { get; set; }
    [Parameter] public string? KindDocumentLabel { get; set; }
    [Parameter] public string? SaveLinkButtonLabel { get; set; }

    private string SaveFirstText => SaveFirstLabel ?? L["Admin.Asset.SaveFirst"];
    private string UploadTabText => UploadTabLabel ?? L["Admin.Asset.UploadTab"];
    private string LinkTabText => LinkTabLabel ?? L["Admin.Asset.LinkTab"];
    private string FileText => FileLabel ?? L["Admin.Asset.File"];
    private string UploadButtonText => UploadButtonLabel ?? L["Admin.Asset.Upload"];
    private string UrlText => UrlLabel ?? L["Admin.Asset.Url"];
    private string KindText => KindLabel ?? L["Admin.Asset.Kind"];
    private string KindImageText => KindImageLabel ?? L["Admin.Asset.Kind.Image"];
    private string KindVideoText => KindVideoLabel ?? L["Admin.Asset.Kind.Video"];
    private string KindDocumentText => KindDocumentLabel ?? L["Admin.Asset.Kind.Document"];
    private string SaveLinkButtonText => SaveLinkButtonLabel ?? L["Admin.Asset.SaveLink"];

    private string PreviewSrc =>
        OwnerId == Guid.Empty || string.IsNullOrEmpty(Category)
            ? string.Empty
            : $"/account/api/admin/assets/{Category}/{OwnerId}/image?v={_version}";

    private async Task UploadAsync()
    {
        if (_busy) return;
        _error = null;
        _success = null;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/assets/{Category}/{OwnerId}/image?kind={_uploadKind}",
                _inputId);
            if (env is { Success: true })
            {
                _version++;
                _success = UploadButtonText;
                await OnChanged.InvokeAsync();
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture() ?? FileText;
            }
        }
        catch
        {
            _error = FileText;
        }
        finally { _busy = false; }
    }

    private async Task SaveLinkAsync()
    {
        if (_busy) return;
        _error = null;
        _success = null;
        if (string.IsNullOrWhiteSpace(_url)) { _error = UrlText; return; }
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson",
                $"/account/api/admin/assets/{Category}/{OwnerId}/link",
                new { Kind = _linkKind, Url = _url.Trim() });
            if (env is { Success: true })
            {
                _version++;
                _url = string.Empty;
                _success = SaveLinkButtonText;
                await OnChanged.InvokeAsync();
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture() ?? UrlText;
            }
        }
        catch
        {
            _error = UrlText;
        }
        finally { _busy = false; }
    }
}
