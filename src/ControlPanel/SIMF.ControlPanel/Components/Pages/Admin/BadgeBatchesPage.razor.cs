using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BadgeBatchesPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminBadgeBatchSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // Re-email modal — the batch being emailed + the editable organiser address
    // (pre-filled with the batch's last recipient).
    private AdminBadgeBatchSummary? _reEmailTarget;
    private string _reEmailRecipient = string.Empty;

    // Revoke confirm modal — the batch about to be disabled.
    private AdminBadgeBatchSummary? _revokeTarget;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OpenReEmail(AdminBadgeBatchSummary row)
    {
        _reEmailTarget = row;
        _reEmailRecipient = row.RecipientEmail ?? string.Empty;
        _toast = null;
    }

    private async Task ReEmailAsync()
    {
        if (_reEmailTarget is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminReEmailBadgeBatchResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/re-email",
                new AdminReEmailBadgeBatchRequest
                {
                    BatchId = _reEmailTarget.Id,
                    RecipientEmail = _reEmailRecipient.Trim(),
                });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.BadgeBatches.ReEmail.Done"],
                        env.Data.BadgeCount, _reEmailRecipient.Trim()));
                _reEmailTarget = null;
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OpenRevoke(AdminBadgeBatchSummary row)
    {
        _revokeTarget = row;
        _toast = null;
    }

    private async Task RevokeAsync()
    {
        if (_revokeTarget is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminRevokeBadgeBatchResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/badge-batches/revoke",
                new AdminRevokeBadgeBatchRequest { BatchId = _revokeTarget.Id });
            if (env is { Success: true, Data: not null })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.BadgeBatches.Revoke.Done"], env.Data.RevokedCount));
                _revokeTarget = null;
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BadgeBatches.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
