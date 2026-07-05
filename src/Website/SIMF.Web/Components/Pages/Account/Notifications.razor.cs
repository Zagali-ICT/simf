using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Notifications;

namespace SIMF.Web.Components.Pages.Account;

// Website — full notification list page (P12 — D-053). The bell links here via
// "View all"; mark-read on the row, delete on the row action, mark-all-read
// from the footer. Reads go through the same-origin BFF JS bridge. Markup lives
// in Notifications.razor.
public partial class Notifications
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 25 };
    private GridPage<NotificationDto> _page = new();
    private bool _loading;
    private Toast? _toast;

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

    private async Task OnDeleteAsync(NotificationDto row)
    {
        await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/notifications/{row.Id}");
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
}
