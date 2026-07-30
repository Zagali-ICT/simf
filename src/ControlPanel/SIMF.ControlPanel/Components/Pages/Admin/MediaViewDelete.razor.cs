using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _busy;
    private bool _confirming;
    private string? _error;

    private async Task ConfirmDeleteAsync()
    {
        if (_busy || Initial is null) return;
        _busy = true;
        _error = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/media/{Initial.Id}");
            if (envelope is { Success: true })
            {
                _confirming = false;
                await OnDeleted.InvokeAsync(Initial);
            }
            else
            {
                // Close the confirm first so the error lands on the visible
                // form body, not behind the (still-open) confirm overlay.
                _confirming = false;
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Media.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _confirming = false;
            _error = L["Admin.Media.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private string KindLabel(MediaKind kind) => kind switch
    {
        MediaKind.Image => L["Admin.Media.Kind.Image"],
        MediaKind.Video => L["Admin.Media.Kind.Video"],
        _ => kind.ToString(),
    };

    // The confirmation needs a human name for the item: the English title,
    // else the Arabic title, else the kind so the message is never blank.
    private string RowName(AdminMediaDetail row) =>
        !string.IsNullOrWhiteSpace(row.Title) ? row.Title!
        : !string.IsNullOrWhiteSpace(row.TitleArabic) ? row.TitleArabic!
        : KindLabel(row.Kind);
}
