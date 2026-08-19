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

    // How far outside a session's [Start, End] window an ARRIVAL is still
    // accepted by the server. The server sends the RESOLVED value per
    // session (its override, else its hall's, else the global one), so there is
    // no constant here to keep in step with HallAttendanceService.
    private static TimeSpan GraceOf(HallArrivalSessionOption session) =>
        TimeSpan.FromMinutes(session.EffectiveArrivalGraceMinutes);

    /// <summary>The picker's rows come from this console's OWN session read,
    /// not the canonical Sessions list.
    ///
    /// <para>This page is gated <c>HallArrivals.View</c>, whose baseline role is
    /// SecurityTeam, and <c>/admin/sessions/list</c> is gated
    /// <c>Sessions.View</c>, which SecurityTeam does not hold. So the console
    /// opened for the operator it exists for and its first fetch 403'd: an empty
    /// picker at a hall door. <c>HallArrivalSessionOption</c> carries only what
    /// the picker needs, including the RESOLVED arrival grace, and rides this
    /// page's own permission.</para></summary>
    private List<HallArrivalSessionOption> _sessions = new();
    private HallArrivalSessionOption? _selected;
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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<HallArrivalSessionOption>>>(
                "simfAccount.postJson", "/account/api/admin/hall-arrivals/sessions/list",
                new GridQuery { Top = 200, Sort = "start" });
            if (envelope is { Success: true, Data: not null })
            {
                // The picker used to apply the ARRIVAL window
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
                    // No IsActive test: the endpoint scopes itself to active
                    // sessions server-side, where no request can widen it.
                    .Where(s => now >= s.Start - GraceOf(s))
                    // Live sessions first (the common check-in case), then the most
                    // recently ended — those are the ones still being closed out.
                    .OrderBy(s => now <= s.End + GraceOf(s) ? 0 : 1)
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
