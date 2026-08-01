using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallArrivalsConsole
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    // X-3 — mirrors HallAttendanceService.ArrivalGrace: how far outside a session's
    // [Start, End] window an ARRIVAL is still accepted by the server.
    private static readonly TimeSpan ArrivalGrace = TimeSpan.FromMinutes(15);

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
                // DEF-CHK-003 — the picker used to apply the ARRIVAL window
                // (EnsureSessionLiveNow, ± ArrivalGrace) to BOTH actions, so once a
                // session had ended the operator could no longer select it — exactly
                // when a hall has to be checked OUT. RecordQrDepartureAsync
                // deliberately has NO window (an attendee already inside must always
                // be able to leave), so any session that has already opened for
                // arrivals stays selectable; only the not-yet-started ones are
                // filtered out (no attendance row can exist for those yet). An
                // arrival attempted on an ended session still gets the server's
                // bilingual SESSION_NOT_LIVE message in the error toast.
                var now = SimfClock.Now;
                _sessions = envelope.Data.Items
                    .Where(s => s.IsActive && now >= s.Start - ArrivalGrace)
                    // Live sessions first (the common check-in case), then the most
                    // recently ended — those are the ones still being closed out.
                    .OrderBy(s => now <= s.End + ArrivalGrace ? 0 : 1)
                    .ThenByDescending(s => s.Start)
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
