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

namespace SIMF.ControlPanel.Components.Pages.Account;

public partial class Notifications
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 25 };
    private GridPage<NotificationDto> _page = new();
    private bool _loading;
    private Toast? _toast;
    private NotificationDto? _detailsTarget;

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
        }
        finally { _loading = false; }
    }

    private Task OnDetailsAsync(NotificationDto row)
    {
        _detailsTarget = row;
        return Task.CompletedTask;
    }

    private async Task OnDeleteAsync(NotificationDto row)
    {
        await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/notifications/{row.Id}");
        await LoadAsync();
    }

    private async Task OnBulkDeleteAsync(IReadOnlyList<NotificationDto> rows)
    {
        // No bulk-dismiss endpoint exists yet; loop the per-row delete.
        // Latency is acceptable — selection caps at the visible page.
        foreach (var row in rows)
        {
            await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/notifications/{row.Id}");
        }
        _toast = new Toast("success",
            string.Format(L["Account.Notifications.BulkDismissed"], rows.Count));
        await LoadAsync();
    }

    private async Task MarkAllReadAsync()
    {
        await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.postJson", "/account/api/notifications/read-all", null);
        _toast = new Toast("success", L["Account.Notifications.MarkAllReadDone"]);
        await LoadAsync();
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
