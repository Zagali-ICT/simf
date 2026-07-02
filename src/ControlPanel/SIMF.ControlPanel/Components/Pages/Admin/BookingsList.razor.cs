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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BookingsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<BookingQueueRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private bool _rejectOpen;
    private Guid _rejectingId;
    private string _rejectReason = string.Empty;

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
            var env = await JS.InvokeAsync<ApiResult<GridPage<BookingQueueRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/bookings/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Bookings.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — bookings are created
    // by visitors in the app and approved/rejected from this queue, so there is
    // no import path. The booking's key is ReservationId.
    private Task OnExportAsync(IReadOnlyList<BookingQueueRow> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/bookings/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.ReservationId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    private async Task ApproveAsync(Guid id)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/bookings/{id}/approve", new { });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.Bookings.Approved"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Bookings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task ApproveSelectedAsync(IReadOnlyList<BookingQueueRow> selected)
    {
        if (_busy || selected.Count == 0) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<int>>(
                "simfAccount.postJson",
                "/account/api/admin/bookings/bulk-approve",
                new AdminBulkApprovalRequest
                {
                    Ids = selected.Select(r => r.ReservationId).ToList(),
                });
            if (env is { Success: true })
            {
                _toast = new Toast("success",
                    string.Format(L["Admin.Bookings.BulkApproved"], env.Data));
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Bookings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OpenReject(BookingQueueRow row)
    {
        _rejectingId = row.ReservationId;
        _rejectReason = string.Empty;
        _rejectOpen = true;
        _toast = null;
    }

    private void OnRejectReasonChanged(ChangeEventArgs e) =>
        _rejectReason = e.Value?.ToString() ?? string.Empty;

    private async Task ConfirmRejectAsync()
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(_rejectReason))
        {
            _toast = new Toast("error", L["Admin.Bookings.ReasonRequired"]);
            return;
        }
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/bookings/{_rejectingId}/reject",
                new RejectBookingRequest { Reason = _rejectReason.Trim() });
            if (env is { Success: true })
            {
                _rejectOpen = false;
                _toast = new Toast("success", L["Admin.Bookings.Rejected"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Bookings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
