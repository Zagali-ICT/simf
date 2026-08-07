using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallSeatLayoutEditor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>A40 — the hall to open, supplied by the "Seat layout" row action on
    /// the Halls grid (<c>/admin/halls/seat-layouts?hallId=…</c>). Null when the page
    /// is reached from the navigation menu, which keeps the picker on its blank entry.
    /// </summary>
    [SupplyParameterFromQuery] public Guid? HallId { get; set; }

    private record Toast(string Variant, string Message);

    // One parsed row: its label + its own seat count. Count is mutable so
    // each per-row number input can write straight back to its RowSeat.
    // Plus its TIER (Normal / VIP / VVIP), likewise mutable so the per-row
    // select writes straight back.
    private sealed record RowSeat
    {
        public string Label { get; init; } = string.Empty;
        public int Count { get; set; }
        public SeatTier Tier { get; set; } = DefaultTier;
    }

    // The default when defining seats at a session must be VVIP reserved:
    // a NEWLY defined row starts VVIP-reserved (nobody may
    // self-book it) and the admin downgrades it to VIP / Normal deliberately.
    private const SeatTier DefaultTier = SeatTier.Vvip;

    // BEM modifier for the preview row's tier band (styles live in the page CSS —
    // no inline style, no raw colour).
    private static string TierModifier(SeatTier tier) => tier switch
    {
        SeatTier.Vvip => "vvip",
        SeatTier.Vip => "vip",
        _ => "normal",
    };

    private List<AdminHallSummary> _halls = new();
    private Guid? _selectedHallId;
    private HallSeatLayoutSnapshot? _snapshot;
    private string _rowLabelsCsv = string.Empty;
    // The per-row seat counts, PARALLEL to the RowLabels text input. The
    // labels text is the row-set source; renaming a label keeps that position's count.
    private List<RowSeat> _rows = new();
    // Default count for a NEWLY added row: the loaded uniform SeatsPerRow (else 1).
    private int _defaultSeatCount = 1;
    private bool _loading;
    private bool _busy;
    private bool _confirmingDelete;
    private Toast? _toast;

    // The server's layout rules, mirrored EXACTLY so the admin sees a refusal
    // while typing instead of a 400 after saving. These match SeatReservationService
    // .SetLayoutAsync (rows 1–26, label 1–8 chars, unique, per-row seats 1–80,
    // sum <= Hall.Capacity) and the EF HallSeatLayoutConfiguration max length.
    private const int MinRows = 1;
    private const int MaxRows = 26;
    private const int MaxRowLabelLength = 8;
    private const int MinSeatsPerRow = 1;
    private const int MaxSeatsPerRow = 80;
    /// <summary>Matches <c>HallSeatLayoutConfiguration</c>'s
    /// <c>Property(x =&gt; x.RowLabels).HasMaxLength(256)</c> — the persisted CSV cap,
    /// also set as the text input's <c>maxlength</c>.</summary>
    private const int MaxRowLabelsCsvLength = 256;

    // -- Per-row seat model + the capacity readout the meter/preview use.
    // _totalSeats is the layout capacity (the sum of the per-row seat counts).
    private int _totalSeats => _rows.Sum(r => r.Count);
    private int _hallCapacity => _snapshot?.HallCapacity ?? 0;
    private bool _anyOutOfRange =>
        _rows.Any(r => r.Count < MinSeatsPerRow || r.Count > MaxSeatsPerRow);
    private bool _isOverCapacity => _hallCapacity > 0 && _totalSeats > _hallCapacity;

    // A40 — every client-side rule violation, in the display order the admin reads
    // the form. Empty means the payload will pass the server's own checks; the server
    // still re-validates (it stays the authority), this only stops the round-trip.
    private List<string> _errors = new();
    private bool _canSave => _errors.Count == 0 && !_busy;
    // A layout can only be removed once one exists (B15).
    private bool _hasStoredLayout => _snapshot is { RowLabels.Count: > 0 };
    // A hall that has no layout and no typed rows yet has not been STARTED, it is not
    // broken — the preview placeholder already says what to do, so the error list stays
    // hidden (Save is still disabled: an empty layout is a server 400, use Remove
    // instead). It appears as soon as the admin types something, or on a hall that
    // already HAS a layout and had its rows cleared.
    private bool _notStarted => _rows.Count == 0 && !_hasStoredLayout;
    private bool _showErrors => _errors.Count > 0 && !_notStarted;

    private static string[] ParseLabels(string csv) =>
        (csv ?? string.Empty).Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A40 — re-run the client-side rules. Called after every edit so the
    /// message list and the Save button state always describe the CURRENT form.</summary>
    private void Revalidate()
    {
        var errors = new List<string>();
        if (_rows.Count < MinRows || _rows.Count > MaxRows)
        {
            errors.Add(string.Format(
                CultureInfo.CurrentCulture,
                L["Admin.HallSeatLayouts.Validation.RowCount"], MinRows, MaxRows));
        }
        if (_rows.Any(r => r.Label.Length > MaxRowLabelLength))
        {
            errors.Add(string.Format(
                CultureInfo.CurrentCulture,
                L["Admin.HallSeatLayouts.Validation.RowLabelLength"], MaxRowLabelLength));
        }
        var labels = _rows.Select(r => r.Label).ToList();
        if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
        {
            errors.Add(L["Admin.HallSeatLayouts.Validation.RowLabelDuplicate"]);
        }
        if (string.Join(',', labels).Length > MaxRowLabelsCsvLength)
        {
            errors.Add(string.Format(
                CultureInfo.CurrentCulture,
                L["Admin.HallSeatLayouts.Validation.RowLabelsTooLong"],
                MaxRowLabelsCsvLength));
        }
        if (_anyOutOfRange)
        {
            errors.Add(string.Format(
                CultureInfo.CurrentCulture,
                L["Admin.HallSeatLayouts.Validation.SeatCount"],
                MinSeatsPerRow, MaxSeatsPerRow));
        }
        if (_isOverCapacity)
        {
            errors.Add(string.Format(
                CultureInfo.CurrentCulture,
                L["Admin.HallSeatLayouts.Validation.Capacity"],
                _totalSeats, _hallCapacity));
        }
        _errors = errors;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadHallsAsync();
        // A40 — honour the ?hallId= the Halls grid's row action navigates with, so the
        // editor opens straight on that hall instead of a blank picker.
        if (HallId is { } fromGrid && _halls.Any(h => h.Id == fromGrid))
        {
            _selectedHallId = fromGrid;
            await LoadLayoutAsync();
        }
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
        _confirmingDelete = false;
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            _selectedHallId = id;
            await LoadLayoutAsync();
        }
        else
        {
            _selectedHallId = null;
            _snapshot = null;
            _rows = new();
            _rowLabelsCsv = string.Empty;
            _errors = new();
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
                _rows = BuildRows(
                    _snapshot.RowLabels, _snapshot.SeatCounts, _snapshot.SeatTiers,
                    _defaultSeatCount);
                Revalidate();
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
        IReadOnlyList<string> labels, IReadOnlyList<int>? counts,
        IReadOnlyList<SeatTier>? tiers, int fallback)
    {
        var rows = new List<RowSeat>(labels.Count);
        for (var i = 0; i < labels.Count; i++)
        {
            var count = counts is not null && i < counts.Count ? counts[i] : fallback;
            // The server always reads back one tier per row (Normal for a layout
            // saved before tiers existed), so an existing hall keeps exactly what it had; only a
            // row with no stored tier at all falls back to the VVIP authoring default.
            var tier = tiers is not null && i < tiers.Count ? tiers[i] : DefaultTier;
            rows.Add(new RowSeat { Label = labels[i], Count = count, Tier = tier });
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
            // A renamed label keeps that position's tier; a brand-new position
            // starts at the VVIP authoring default.
            var tier = i < _rows.Count ? _rows[i].Tier : DefaultTier;
            reconciled.Add(new RowSeat { Label = labels[i], Count = count, Tier = tier });
        }
        _rows = reconciled;
        Revalidate();
    }

    private void OnRowTierChanged(int index, ChangeEventArgs e)
    {
        if (index < 0 || index >= _rows.Count) return;
        if (int.TryParse(e.Value?.ToString(), out var raw)
            && Enum.IsDefined(typeof(SeatTier), raw))
        {
            _rows[index].Tier = (SeatTier)raw;
        }
    }

    private void OnRowCountChanged(int index, ChangeEventArgs e)
    {
        if (index < 0 || index >= _rows.Count) return;
        // Store the raw value (do NOT clamp) so an out-of-range entry surfaces the
        // A40 message that mirrors the server-side 1..80 check.
        _rows[index].Count = int.TryParse(e.Value?.ToString(), out var n) ? n : 0;
        Revalidate();
    }

    private async Task SaveAsync()
    {
        if (_selectedHallId is null || _busy) return;
        // A40 — re-run the rules on submit as well as on edit: the button is already
        // disabled while invalid, so this only guards a programmatic/stale call.
        Revalidate();
        if (_errors.Count > 0) return;
        _busy = true;
        _toast = null;
        try
        {
            // _rows is the single source for both arrays, so RowLabels and
            // SeatCounts are always parallel and the same length.
            var labels = _rows.Select(r => r.Label).ToArray();
            var counts = _rows.Select(r => r.Count).ToArray();
            var tiers = _rows.Select(r => r.Tier).ToArray();
            var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
                "simfAccount.putJson",
                $"/account/api/admin/halls/{_selectedHallId}/seat-layout",
                new SetHallSeatLayoutRequest
                {
                    RowLabels = labels,
                    // Keep max(counts) as the legacy uniform fallback (empty => 1).
                    SeatsPerRow = counts.Length > 0 ? counts.Max() : 1,
                    SeatCounts = counts,
                    // Always explicit, so the editor's tier choices (and the
                    // VVIP default on a new row) are what gets persisted.
                    SeatTiers = tiers,
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

    // -- Remove the layout (the hall reverts to general admission) -------------

    private void OpenDeleteConfirm()
    {
        _toast = null;
        _confirmingDelete = true;
    }

    private void CancelDeleteConfirm() => _confirmingDelete = false;

    private async Task DeleteAsync()
    {
        if (_selectedHallId is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<HallSeatLayoutSnapshot>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/halls/{_selectedHallId}/seat-layout");
            if (env is { Success: true, Data: not null })
            {
                // The server returns the now-empty snapshot; mirror it so the preview,
                // the capacity meter and the Delete button all clear in one render.
                _snapshot = env.Data;
                _rowLabelsCsv = string.Empty;
                _defaultSeatCount = 1;
                _rows = new();
                _confirmingDelete = false;
                Revalidate();
                _toast = new Toast("success", L["Admin.HallSeatLayouts.Deleted"]);
            }
            else
            {
                // A 409 SEAT_LAYOUT_HAS_RESERVATIONS names how many bookings block the
                // removal — surface the server's own bilingual text, not a generic one.
                _confirmingDelete = false;
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.HallSeatLayouts.DeleteFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
