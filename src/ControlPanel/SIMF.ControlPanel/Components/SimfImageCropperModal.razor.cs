using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Cropper.Blazor.Components;
using Cropper.Blazor.Models;

namespace SIMF.ControlPanel.Components;

public partial class SimfImageCropperModal
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private ILogger<SimfImageCropperModal> Logger { get; set; } = default!;

    private CropperComponent? _cropper;
    private bool _submitting;
    private Options _options = default!;

    /// <summary>True to render the modal.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>The source image — URL or data URL.</summary>
    [Parameter, EditorRequired] public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Optional dialog title; defaults to the localized "Crop image".</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Crop aspect ratio (width / height). Default 1 (square).</summary>
    [Parameter] public decimal AspectRatio { get; set; } = 1m;

    /// <summary>Output width in pixels. Default 400.</summary>
    [Parameter] public int OutputWidth { get; set; } = 400;

    /// <summary>Output height in pixels. Default 400.</summary>
    [Parameter] public int OutputHeight { get; set; } = 400;

    /// <summary>Output mime type. Default "image/png".</summary>
    [Parameter] public string OutputMimeType { get; set; } = "image/png";

    /// <summary>Fill colour behind transparent regions when flattening. Default white.</summary>
    [Parameter] public string FillColor { get; set; } = "#ffffff";

    /// <summary>Raised on successful crop with a data:image/...;base64 URL string.</summary>
    [Parameter] public EventCallback<string> OnCropped { get; set; }

    /// <summary>Raised when the user cancels the dialog.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    protected override void OnParametersSet()
    {
        // Options block byte-identical to V10 UserLogoCropperDialog,
        // with AspectRatio / InitialAspectRatio surfaced as a parameter so
        // future surfaces can opt into non-square crops.
        _options = new Options
        {
            AspectRatio = AspectRatio,
            InitialAspectRatio = AspectRatio,

            ViewMode = ViewMode.Vm1,
            DragMode = "move",

            Background = true,
            Modal = true,

            AutoCrop = true,
            AutoCropArea = 0.9m,

            Guides = true,
            Center = true,
            Highlight = true,

            CropBoxMovable = true,
            CropBoxResizable = true,

            Responsive = true,
            Restore = true,

            Zoomable = true,
            ZoomOnTouch = true,
            ZoomOnWheel = true,
            WheelZoomRatio = 0.1m,

            CheckOrientation = true,
            CheckCrossOrigin = true,
        };
    }

    private async Task OnCancelInternal()
    {
        if (_submitting) return;
        if (OnCancel.HasDelegate) await OnCancel.InvokeAsync();
    }

    private async Task OnSubmitInternal()
    {
        if (_submitting) return;
        if (_cropper is null)
        {
            await OnCancelInternal();
            return;
        }

        _submitting = true;
        StateHasChanged();

        try
        {
            // Canvas-extract logic byte-identical to V10 UserLogoCropperDialog.
            var receiver = await _cropper.GetCroppedCanvasDataInBackgroundAsync(
                new GetCroppedCanvasOptions
                {
                    Width = OutputWidth,
                    Height = OutputHeight,
                    FillColor = FillColor,
                    ImageSmoothingEnabled = true,
                    ImageSmoothingQuality = "high",
                });

            using MemoryStream ms = await receiver.GetImageChunkStreamAsync();
            byte[] bytes = ms.ToArray();

            if (bytes.Length == 0)
            {
                await OnCancelInternal();
                return;
            }

            string dataUrl = $"data:{OutputMimeType};base64," + Convert.ToBase64String(bytes);
            if (OnCropped.HasDelegate) await OnCropped.InvokeAsync(dataUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to crop image");
            await OnCancelInternal();
        }
        finally
        {
            _submitting = false;
            StateHasChanged();
        }
    }
}
