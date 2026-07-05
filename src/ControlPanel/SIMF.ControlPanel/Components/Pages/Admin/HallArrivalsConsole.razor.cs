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
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallArrivalsConsole
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummary> _sessions = new();
    private AdminSessionSummary? _selected;
    private string _qrId = string.Empty;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadSessionsAsync();

    private async Task LoadSessionsAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.postJson", "/account/api/admin/sessions/list",
                new GridQuery { Top = 200, Sort = "startUtc" });
            if (envelope is { Success: true, Data: not null })
            {
                _sessions = envelope.Data.Items.Where(s => s.IsActive).ToList();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallArrivals.Fallback"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task RecordAsync()
    {
        if (_selected is null) { _toast = new Toast("error", L["Admin.HallArrivals.NeedSession"]); return; }
        if (string.IsNullOrWhiteSpace(_qrId)) { return; }

        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<QrArrivalResult>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selected.Id}/arrivals",
                new RecordQrArrivalRequest { QrId = _qrId.Trim() });
            if (envelope is { Success: true, Data: not null })
            {
                _toast = new Toast("success", $"{L["Admin.HallArrivals.Recorded"]}: {envelope.Data.DisplayName}");
                _qrId = string.Empty; // ready for the next scan
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallArrivals.Fallback"]);
            }
        }
        finally { _busy = false; }
    }
}
