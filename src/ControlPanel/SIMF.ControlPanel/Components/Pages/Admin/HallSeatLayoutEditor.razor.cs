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

    // D-767 — one parsed row: its label + its own seat count. Count is mutable so
    // each per-row number input can write straight back to its RowSeat.
    private sealed record RowSeat
    {
        public string Label { get; init; } = string.Empty;
        public int Count { get; set; }
    }

    private List<AdminHallSummary> _halls = new();
    private Guid? _selectedHallId;
    private HallSeatLayoutSnapshot? _snapshot;
    private string _rowLabelsCsv = string.Empty;
    // D-767 — the per-row seat counts, PARALLEL to the RowLabels text input. The
    // labels text is the row-set source; renaming a label keeps that position's count.
    private List<RowSeat> _rows = new();
    // Default count for a NEWLY added row: the loaded uniform SeatsPerRow (else 1).
    private int _defaultSeatCount = 1;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // -- Per-row seat model (D-767) + the capacity readout the meter/preview use.
    // _totalSeats is the layout capacity (the sum of the per-row seat counts).
    private int _totalSeats => _rows.Sum(r => r.Count);
    private int _hallCapacity => _snapshot?.HallCapacity ?? 0;
    private bool _anyOutOfRange => _rows.Any(r => r.Count < 1 || r.Count > 80);
    // Visual-only warning — Save stays enabled so the server remains the single
    // source of truth for the capacity rule (SEAT_CAPACITY_EXCEEDED).
    private bool _isOverCapacity => _hallCapacity > 0 && _totalSeats > _hallCapacity;

    private static string[] ParseLabels(string csv) =>
        (csv ?? string.Empty).Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
                // New-row default = the loaded uniform SeatsPerRow (else 1).
                _defaultSeatCount = _snapshot.SeatsPerRow > 0 ? _snapshot.SeatsPerRow : 1;
                _rows = BuildRows(_snapshot.RowLabels, _snapshot.SeatCounts, _defaultSeatCount);
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

    // Build the per-row list from the snapshot: each row takes its own SeatCounts
    // entry when present (variable layout), else falls back to the uniform count.
    private static List<RowSeat> BuildRows(
        IReadOnlyList<string> labels, IReadOnlyList<int>? counts, int fallback)
    {
        var rows = new List<RowSeat>(labels.Count);
        for (var i = 0; i < labels.Count; i++)
        {
            var count = counts is not null && i < counts.Count ? counts[i] : fallback;
            rows.Add(new RowSeat { Label = labels[i], Count = count });
        }
        return rows;
    }

    // The RowLabels text is the row-set source. Reconcile _rows POSITIONALLY so a
    // renamed label keeps that position's seat count; a new position gets the default.
    private void OnRowLabelsChanged(ChangeEventArgs e)
    {
        _rowLabelsCsv = e.Value?.ToString() ?? string.Empty;
        var labels = ParseLabels(_rowLabelsCsv);
        var reconciled = new List<RowSeat>(labels.Length);
        for (var i = 0; i < labels.Length; i++)
        {
            var count = i < _rows.Count ? _rows[i].Count : _defaultSeatCount;
            reconciled.Add(new RowSeat { Label = labels[i], Count = count });
        }
        _rows = reconciled;
    }

    private void OnRowCountChanged(int index, ChangeEventArgs e)
    {
        if (index < 0 || index >= _rows.Count) return;
        // Store the raw value (do NOT clamp) so an out-of-range entry surfaces the
        // warning, mirroring the server-side 1..80 check (Save stays enabled).
        _rows[index].Count = int.TryParse(e.Value?.ToString(), out var n) ? n : 0;
    }

    private async Task SaveAsync()
    {
        if (_selectedHallId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            // _rows is the single source for both arrays, so RowLabels and
            // SeatCounts are always parallel and the same length.
            var labels = _rows.Select(r => r.Label).ToArray();
            var counts = _rows.Select(r => r.Count).ToArray();
            var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
                "simfAccount.putJson",
                $"/account/api/admin/halls/{_selectedHallId}/seat-layout",
                new SetHallSeatLayoutRequest
                {
                    RowLabels = labels,
                    // Keep max(counts) as the legacy uniform fallback (empty => 1).
                    SeatsPerRow = counts.Length > 0 ? counts.Max() : 1,
                    SeatCounts = counts,
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
