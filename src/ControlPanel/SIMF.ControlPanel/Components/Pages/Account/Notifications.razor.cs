using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components.Pages.Account;

public partial class Notifications
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [CascadingParameter(Name = "BellRefresh")]
    private Func<Task>? BellRefresh { get; set; }

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 25 };
    private GridPage<NotificationDto> _page = new();
    private bool _loading;
    private Toast? _toast;
    private NotificationDto? _detailsTarget;

    // D-809 — the grid's trash icon and bulk-dismiss button both used to delete on
    // the first click. One dialog serves both, so it carries the action to run.
    private (string Title, string Message, Func<Task> Run)? _pendingDismiss;

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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<NotificationDto>>>(
                "simfAccount.postJson", "/account/api/notifications/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                // §6.16 (F-U5-006) — a failed fetch used to leave the last page
                // on screen with no hint it was stale.
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Account.Notifications.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnDetailsAsync(NotificationDto row)
    {
        if (row.ReadAt is null)
        {
            // §6.16 (F-U5-006) — only paint the row as read if the server
            // actually recorded it. The optimistic update used to run
            // unconditionally, so a failed mark-read left the list showing
            // "read" while the bell count stayed stubbornly unchanged.
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson", $"/account/api/notifications/{row.Id}/read", null);
            if (env is { Success: true })
            {
                var updated = row with { ReadAt = SimfClock.Now, IsRead = true };
                _page = new GridPage<NotificationDto>
                {
                    Items = _page.Items.Select(n => n.Id == row.Id ? updated : n).ToList(),
                    Total = _page.Total,
                    Skip = _page.Skip,
                    Top = _page.Top,
                };
                if (BellRefresh is not null) await BellRefresh();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Account.Notifications.MarkReadFailed"]);
            }
        }
        _detailsTarget = row;
    }

    private void AskDismiss(string title, string message, Func<Task> run)
    {
        _pendingDismiss = (title, message, run);
        _toast = null;
    }

    private void CancelDismiss() => _pendingDismiss = null;

    /// <summary>Closes the dialog before running so the resulting toast is visible.</summary>
    private async Task ConfirmDismissAsync()
    {
        var pending = _pendingDismiss;
        _pendingDismiss = null;
        if (pending is not null) await pending.Value.Run();
    }

    private async Task OnDeleteAsync(NotificationDto row)
    {
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/notifications/{row.Id}");
        if (env is not { Success: true })
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Account.Notifications.DismissFailed"]);
        }
        await LoadAsync();
        if (BellRefresh is not null) await BellRefresh();
    }

    private async Task OnBulkDeleteAsync(IReadOnlyList<NotificationDto> rows)
    {
        // No bulk-dismiss endpoint exists yet; loop the per-row delete.
        // Latency is acceptable — selection caps at the visible page.
        //
        // §6.16 (F-U5-006) — every per-row result used to be discarded and the
        // page reported "N dismissed" unconditionally, so an admin whose N
        // deletes ALL failed was told they had all succeeded. Count them.
        var dismissed = 0;
        string? firstError = null;
        foreach (var row in rows)
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/notifications/{row.Id}");
            if (env is { Success: true })
            {
                dismissed++;
            }
            else
            {
                firstError ??= env?.Error?.MessageForCurrentCulture();
            }
        }

        if (dismissed == rows.Count)
        {
            _toast = new Toast("success",
                string.Format(L["Account.Notifications.BulkDismissed"], dismissed));
        }
        else if (dismissed == 0)
        {
            _toast = new Toast("error",
                firstError ?? L["Account.Notifications.DismissFailed"]);
        }
        else
        {
            // A partial result is its own outcome — saying "12 dismissed" when
            // 3 of them are still there is the same lie in a smaller size.
            // "error" (not info): SimfAlert has no warning variant, and some of
            // what the admin asked for did not happen, so it is announced
            // assertively rather than styled as a neutral note.
            _toast = new Toast("error",
                string.Format(L["Account.Notifications.BulkDismissedPartial"],
                    dismissed, rows.Count));
        }

        await LoadAsync();
        if (BellRefresh is not null) await BellRefresh();
    }

    private async Task MarkAllReadAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.postJson", "/account/api/notifications/read-all", null);
        _toast = env is { Success: true }
            ? new Toast("success", L["Account.Notifications.MarkAllReadDone"])
            : new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Account.Notifications.MarkReadFailed"]);
        await LoadAsync();
        if (BellRefresh is not null) await BellRefresh();
    }

    private string TitleFor(NotificationDto n) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? n.TitleArabic : n.Title;

    private string BodyFor(NotificationDto n) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? n.BodyArabic : n.Body;

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Account.Notifications.Summary"],
            skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Account.Notifications.Pager.Page"], current, total);
}
