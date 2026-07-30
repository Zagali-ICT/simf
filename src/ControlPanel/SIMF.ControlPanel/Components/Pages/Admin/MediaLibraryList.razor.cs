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
using SIMF.Contracts.Gates;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Assets;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaLibraryList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminAssetSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;
    private AdminAssetSummary? _detailsTarget;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAssetSummary>>>(
                "simfAccount.postJson", "/account/api/admin/assets/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MediaLibrary.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnDetailsAsync(AdminAssetSummary row)
    {
        var env = await JS.InvokeAsync<ApiResult<AdminAssetSummary>>(
            "simfAccount.getJson", $"/account/api/admin/assets/item/{row.Id}");
        if (env is { Success: true, Data: not null })
        {
            _detailsTarget = env.Data;
        }
        else
        {
            // §6.16 (F-U5-011) — a failed detail fetch used to do literally
            // nothing: no modal, no message, no spinner. The Manage button was
            // indistinguishable from an unwired one.
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.MediaLibrary.LoadFailed"]);
        }
    }

    // D-799 — deactivating takes a live asset (a speaker photo, a sponsor logo)
    // off the public site and used to commit on the first click. Staging the
    // target closes the details modal, so the confirm is never a stacked dialog.
    private AdminAssetSummary? _deactivateTarget;

    private void AskDeactivate()
    {
        _deactivateTarget = _detailsTarget;
        _detailsTarget = null;
    }

    private void CancelDeactivate() => _deactivateTarget = null;

    private async Task DeactivateAsync()
    {
        if (_busy || _deactivateTarget is null) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/assets/item/{_deactivateTarget.Id}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.MediaLibrary.Deactivated"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MediaLibrary.LoadFailed"]);
            }
        }
        finally
        {
            // Close on BOTH paths: the toast renders on the page behind the
            // dialog, so leaving it open on failure hides the reason.
            _deactivateTarget = null;
            _busy = false;
        }
    }

    private async Task RestoreAsync()
    {
        if (_busy || _detailsTarget is null) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson", $"/account/api/admin/assets/item/{_detailsTarget.Id}/restore", new { });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.MediaLibrary.Restored"]);
                _detailsTarget = null;
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MediaLibrary.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private static string PreviewSrc(AdminAssetSummary a) =>
        $"/account/api/admin/assets/{a.Category}/{a.OwnerId}/image";

    private string TypeLabel(AdminAssetSummary a) =>
        a.SourceType == SIMF.Common.Enums.AssetSourceType.ExternalLink
            ? L["Admin.MediaLibrary.Type.Link"]
            : L["Admin.MediaLibrary.Type.Upload"];

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);
}
