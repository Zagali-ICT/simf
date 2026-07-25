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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallSeatLayoutEditor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminHallSummary> _halls = new();
    private Guid? _selectedHallId;
    private HallSeatLayoutSnapshot? _snapshot;
    private string _rowLabelsCsv = string.Empty;
    private int _seatsPerRow = 1;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // The row labels the admin has typed, trimmed and with blanks dropped —
    // the same parse the Save uses, shared so the live preview and the
    // capacity readout can never disagree with what gets persisted.
    private IReadOnlyList<string> _previewRowLabels =>
        string.IsNullOrWhiteSpace(_rowLabelsCsv)
            ? Array.Empty<string>()
            : _rowLabelsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

    private int _currentRowCount => _previewRowLabels.Count;

    private int _layoutCapacity => _currentRowCount * _seatsPerRow;

    private int _hallCapacity => _snapshot?.HallCapacity ?? 0;

    // Visual-only warning — Save stays enabled so the server remains the single
    // source of truth for the capacity rule (SEAT_CAPACITY_EXCEEDED).
    private bool _isOverCapacity => _hallCapacity > 0 && _layoutCapacity > _hallCapacity;

    protected override async Task OnInitializedAsync()
    {
        await LoadHallsAsync();
    }

    private async Task LoadHallsAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
                "simfAccount.postJson", "/account/api/admin/halls/list",
                new GridQuery { Top = 200 });
            if (env is { Success: true, Data: not null })
            {
                _halls = env.Data.Items.Where(h => h.IsActive)
                    .OrderBy(h => h.Code).ToList();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallSeatLayouts.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnHallChangedAsync(ChangeEventArgs e)
    {
        // Clear any stale toast so a "Saved" / "LoadFailed" message
        // from hall A doesn't follow the admin to hall B.
        _toast = null;
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            _selectedHallId = id;
            await LoadLayoutAsync();
        }
        else
        {
            _selectedHallId = null;
            _snapshot = null;
        }
    }

    private async Task LoadLayoutAsync()
    {
        if (_selectedHallId is null) return;
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
                "simfAccount.getJson",
                $"/account/api/admin/halls/{_selectedHallId}/seat-layout");
            if (env is { Success: true, Data: not null })
            {
                _snapshot = env.Data;
                _rowLabelsCsv = string.Join(',', _snapshot.RowLabels);
                _seatsPerRow = _snapshot.SeatsPerRow > 0 ? _snapshot.SeatsPerRow : 1;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallSeatLayouts.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OnRowLabelsChanged(ChangeEventArgs e) =>
        _rowLabelsCsv = e.Value?.ToString() ?? string.Empty;

    private void OnSeatsPerRowChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n))
        {
            _seatsPerRow = n;
        }
    }

    private async Task SaveAsync()
    {
        if (_selectedHallId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var rows = (_rowLabelsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .ToArray();
            var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
                "simfAccount.putJson",
                $"/account/api/admin/halls/{_selectedHallId}/seat-layout",
                new SetHallSeatLayoutRequest
                {
                    RowLabels = rows,
                    SeatsPerRow = _seatsPerRow,
                });
            if (env is { Success: true, Data: not null })
            {
                _snapshot = env.Data;
                _toast = new Toast("success", L["Admin.HallSeatLayouts.Saved"]);
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallSeatLayouts.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
