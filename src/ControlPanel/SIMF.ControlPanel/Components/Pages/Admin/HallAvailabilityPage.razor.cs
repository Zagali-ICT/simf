using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallAvailabilityPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminHallSummary> _halls = new();
    private List<AdminHallAvailabilityWindow> _windows = new();
    private Guid? _hallId;
    private string _start = string.Empty;
    private string _end = string.Empty;
    private string _slotMinutes = "30";
    private bool _busy;
    private Toast? _toast;

    // R10 (D-767) — a must-decide guard for the destructive window delete.
    private bool _confirmOpen;
    private Guid _confirmWindowId;

    protected override async Task OnInitializedAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (envelope is { Success: true, Data: not null })
        {
            // Business meetings are hosted in Meeting (or General) halls — compare
            // via the enum so a wire-int reorder can't drift (mirrors BusinessMeetingsList).
            _halls = envelope.Data.Items
                .Where(h => h.IsActive
                    && ((HallPurpose)h.Purpose) is HallPurpose.Meeting or HallPurpose.General)
                .OrderBy(h => h.Name).ToList();
        }
    }

    private async Task OnHallChanged(ChangeEventArgs e)
    {
        _toast = null;
        _hallId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        await LoadWindowsAsync();
    }

    private async Task LoadWindowsAsync()
    {
        if (_hallId is not { } id) { _windows = new(); return; }
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminHallAvailabilityWindow>>>(
            "simfAccount.getJson", $"/account/api/admin/halls/{id}/availability-windows");
        _windows = envelope is { Success: true, Data: not null }
            ? envelope.Data.ToList()
            : new();
    }

    private async Task AddWindowAsync()
    {
        if (_hallId is not { } id) { return; }
        if (!TryParseUtc(_start, out var start) || !TryParseUtc(_end, out var end))
        {
            _toast = new Toast("error", L["Admin.HallAvailability.BadDates"]);
            return;
        }
        if (!int.TryParse(_slotMinutes, out var slot) || slot <= 0)
        {
            _toast = new Toast("error", L["Admin.HallAvailability.BadSlot"]);
            return;
        }

        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminHallAvailabilityWindow>>(
                "simfAccount.postJson",
                $"/account/api/admin/halls/{id}/availability-windows",
                new CreateHallAvailabilityWindowRequest
                {
                    Start = start, End = end, SlotMinutes = slot,
                });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.HallAvailability.Added"]);
                _start = _end = string.Empty;
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    // R10 (D-767) — open the delete confirm; RunDeleteAsync does the work on OK.
    private void ConfirmDelete(Guid windowId)
    {
        _confirmWindowId = windowId;
        _confirmOpen = true;
        _toast = null;
    }

    private async Task RunDeleteAsync()
    {
        _confirmOpen = false;
        await DeleteWindowAsync(_confirmWindowId);
    }

    private void CancelConfirm() => _confirmOpen = false;

    private async Task DeleteWindowAsync(Guid windowId)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/hall-availability-windows/{windowId}");
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L["Admin.Availability.WindowDeleted"]);
                await LoadWindowsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallAvailability.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private static bool TryParseUtc(string value, out DateTime result)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            result = SaudiTime.FromSaudiWallClock(dt);
            return true;
        }
        result = default;
        return false;
    }
}
