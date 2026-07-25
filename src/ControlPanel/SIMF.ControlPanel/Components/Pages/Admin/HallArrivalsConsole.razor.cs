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
                new GridQuery { Top = 200, Sort = "start" });
            if (envelope is { Success: true, Data: not null })
            {
                // X-3 — the operator can only record an arrival against a session
                // that is currently live (its time window, ± a short grace). Match
                // the server's EnsureSessionLiveNow rule so the picker never offers
                // a session the API would reject with SESSION_NOT_LIVE.
                var now = DateTimeOffset.UtcNow;
                var grace = TimeSpan.FromMinutes(15);
                _sessions = envelope.Data.Items
                    .Where(s => s.IsActive
                        && now >= s.Start - grace
                        && now <= s.End + grace)
                    .ToList();
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

    // 2026-07-18 — staff check-OUT: scan the badge QR to close the attendee's open
    // attendance row for the selected session (the seat map's confirmed state clears).
    private async Task DepartAsync()
    {
        if (_selected is null) { _toast = new Toast("error", L["Admin.HallArrivals.NeedSession"]); return; }
        if (string.IsNullOrWhiteSpace(_qrId)) { return; }

        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<QrArrivalResult>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selected.Id}/departures",
                new RecordQrArrivalRequest { QrId = _qrId.Trim() });
            if (envelope is { Success: true, Data: not null })
            {
                _toast = new Toast("success", $"{L["Admin.HallArrivals.CheckedOut"]}: {envelope.Data.DisplayName}");
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
