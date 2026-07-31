using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MeetingTablesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private sealed class TableForm
    {
        public string Code { get; set; } = string.Empty;
        public string RowLabel { get; set; } = string.Empty;
        public int? ColumnNumber { get; set; }
        public int Capacity { get; set; } = 2;
    }

    private List<AdminHallSummary> _halls = new();
    private Guid? _hallId;
    private int _purpose;
    private GridQuery _tableQuery = new() { Top = 20 };
    private GridPage<MeetingTableRow> _tablePage = new();
    private bool _tableLoading;

    private GridQuery _allocQuery = new() { Top = 20 };
    private GridPage<HallAllocationRow> _allocPage = new();
    private bool _allocLoading;

    private bool _busy;
    private Toast? _toast;

    private bool _tableOpen;
    private bool _isEditTable;
    private Guid _editingTableId;
    private TableForm _tableForm = new();

    private bool _generateOpen;
    private GenerateMeetingTablesRequest _genForm = new();

    private bool _allocOpen;
    private CreateHallAllocationRequest _allocForm = new();
    private string _allocStartText = string.Empty;
    private string _allocEndText = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadHallsAsync();

    private async Task LoadHallsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list", new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _halls = env.Data.Items.Where(h => h.IsActive).OrderBy(h => h.Name).ToList();
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
        }
    }

    private async Task OnHallChangedAsync(ChangeEventArgs e)
    {
        _hallId = Guid.TryParse(e.Value?.ToString(), out var g) ? g : null;
        _toast = null;
        if (_hallId is { } hid)
        {
            _purpose = _halls.FirstOrDefault(h => h.Id == hid)?.Purpose ?? 0;
            _tableQuery = new GridQuery { Top = _tableQuery.Top };
            _allocQuery = new GridQuery { Top = _allocQuery.Top };
            await LoadTablesAsync();
            await LoadAllocationsAsync();
        }
    }

    private void OnPurposeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _purpose = v;
    }

    private async Task SavePurposeAsync()
    {
        if (_busy || _hallId is not { } hid) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson", $"/account/api/admin/halls/{hid}/purpose",
                new SetHallPurposeRequest { Purpose = (HallPurpose)_purpose });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.MeetingTables.Purpose.Saved"]);
                // reflect the new purpose in the cached hall list
                var idx = _halls.FindIndex(h => h.Id == hid);
                if (idx >= 0) _halls[idx] = _halls[idx] with { Purpose = _purpose };
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task OnTableQueryChangedAsync(GridQuery next)
    {
        _tableQuery = next;
        await LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        if (_hallId is not { } hid) return;
        _tableLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<MeetingTableRow>>>(
                "simfAccount.postJson", $"/account/api/admin/halls/{hid}/meeting-tables/list", _tableQuery);
            if (env is { Success: true, Data: not null }) _tablePage = env.Data;
        }
        finally { _tableLoading = false; }
    }

    private async Task OnAllocQueryChangedAsync(GridQuery next)
    {
        _allocQuery = next;
        await LoadAllocationsAsync();
    }

    private async Task LoadAllocationsAsync()
    {
        if (_hallId is not { } hid) return;
        _allocLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<HallAllocationRow>>>(
                "simfAccount.postJson", $"/account/api/admin/halls/{hid}/hall-allocations/list", _allocQuery);
            if (env is { Success: true, Data: not null }) _allocPage = env.Data;
        }
        finally { _allocLoading = false; }
    }

    private void OnAddTable()
    {
        _isEditTable = false;
        _editingTableId = Guid.Empty;
        _tableForm = new TableForm();
        _tableOpen = true;
        _toast = null;
    }

    private void OnEditTable(MeetingTableRow row)
    {
        _isEditTable = true;
        _editingTableId = row.Id;
        _tableForm = new TableForm
        {
            Code = row.Code,
            RowLabel = row.RowLabel ?? string.Empty,
            ColumnNumber = row.ColumnNumber,
            Capacity = row.Capacity,
        };
        _tableOpen = true;
        _toast = null;
    }

    private async Task SaveTableAsync()
    {
        if (_busy || _hallId is not { } hid) return;
        if (string.IsNullOrWhiteSpace(_tableForm.Code))
        {
            _toast = new Toast("error", L["Admin.MeetingTables.Validation.Code"]);
            return;
        }
        _busy = true;
        try
        {
            ApiResult<MeetingTableRow>? env;
            if (_isEditTable)
            {
                env = await JS.InvokeAsync<ApiResult<MeetingTableRow>>(
                    "simfAccount.putJson", $"/account/api/admin/meeting-tables/{_editingTableId}",
                    new UpdateMeetingTableRequest
                    {
                        Code = _tableForm.Code,
                        RowLabel = Trimmed(_tableForm.RowLabel),
                        ColumnNumber = _tableForm.ColumnNumber,
                        Capacity = _tableForm.Capacity,
                    });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<MeetingTableRow>>(
                    "simfAccount.postJson", $"/account/api/admin/halls/{hid}/meeting-tables",
                    new CreateMeetingTableRequest
                    {
                        Code = _tableForm.Code,
                        RowLabel = Trimmed(_tableForm.RowLabel),
                        ColumnNumber = _tableForm.ColumnNumber,
                        Capacity = _tableForm.Capacity,
                    });
            }
            if (env is { Success: true })
            {
                _tableOpen = false;
                _toast = new Toast("success", L["Admin.MeetingTables.Saved"]);
                await LoadTablesAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task OnDeleteTableAsync(MeetingTableRow row)
    {
        if (_busy) return;
        var confirmed = await JS.InvokeAsync<bool>("confirm", L["Admin.MeetingTables.Delete.Confirm"].Value);
        if (!confirmed) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/meeting-tables/{row.Id}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.MeetingTables.Deleted"]);
                await LoadTablesAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    // D-356 — Excel export (selected rows, or the current filtered set) of the
    // hall's meeting tables. The grid is hall-scoped, so the hall id rides
    // GridQuery.Filters["hallId"]; the API resolves it back to ListTablesAsync.
    // No hall selected → nothing to export.
    private async Task OnExportTablesAsync(IReadOnlyList<MeetingTableRow> selected)
    {
        if (_hallId is not { } hid) return;
        var query = new GridQuery
        {
            Skip = 0,
            Top = _tableQuery.Top,
            Search = _tableQuery.Search,
            Sort = _tableQuery.Sort,
            SortDescending = _tableQuery.SortDescending,
            Filters = new Dictionary<string, string>(_tableQuery.Filters)
            {
                ["hallId"] = hid.ToString(),
            },
        };
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/meeting-tables/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = query,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private void OnGenerate()
    {
        _genForm = new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RandomByCount, Capacity = 2 };
        _generateOpen = true;
        _toast = null;
    }

    private async Task GenerateAsync()
    {
        if (_busy || _hallId is not { } hid) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<MeetingTablesGenerated>>(
                "simfAccount.postJson", $"/account/api/admin/halls/{hid}/meeting-tables/generate", _genForm);
            if (env is { Success: true })
            {
                _generateOpen = false;
                _toast = new Toast("success", L["Admin.MeetingTables.Generated"]);
                await LoadTablesAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OnAddAllocation()
    {
        _allocForm = new CreateHallAllocationRequest
        {
            Purpose = HallPurpose.Meeting,
            Mode = HallAllocationMode.Whole,
        };
        _allocStartText = string.Empty;
        _allocEndText = string.Empty;
        _allocOpen = true;
        _toast = null;
    }

    private async Task SaveAllocationAsync()
    {
        if (_busy || _hallId is not { } hid) return;
        if (!TryParseUtc(_allocStartText, out var start) || !TryParseUtc(_allocEndText, out var end) || end <= start)
        {
            _toast = new Toast("error", L["Admin.MeetingTables.Validation.Slot"]);
            return;
        }
        if ((_allocForm.Mode == HallAllocationMode.RandomByCount && _allocForm.UnitCount is not > 0)
            || (_allocForm.Mode == HallAllocationMode.RowColumn
                && string.IsNullOrWhiteSpace(_allocForm.RowColumnSpec)))
        {
            _toast = new Toast("error", L["Admin.MeetingTables.Validation.Allocation"]);
            return;
        }
        _busy = true;
        try
        {
            _allocForm.Start = start;
            _allocForm.End = end;
            var env = await JS.InvokeAsync<ApiResult<HallAllocationRow>>(
                "simfAccount.postJson", $"/account/api/admin/halls/{hid}/hall-allocations", _allocForm);
            if (env is { Success: true })
            {
                _allocOpen = false;
                _toast = new Toast("success", L["Admin.MeetingTables.Saved"]);
                await LoadAllocationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task OnReleaseAllocationAsync(HallAllocationRow row)
    {
        if (_busy) return;
        var confirmed = await JS.InvokeAsync<bool>("confirm", L["Admin.MeetingTables.Release.Confirm"].Value);
        if (!confirmed) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson", $"/account/api/admin/hall-allocations/{row.Id}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.MeetingTables.Released"]);
                await LoadAllocationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.MeetingTables.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OnColumnChanged(ChangeEventArgs e) =>
        _tableForm.ColumnNumber = int.TryParse(e.Value?.ToString(), out var n) ? n : null;
    private void OnCapacityChanged(ChangeEventArgs e) =>
        _tableForm.Capacity = int.TryParse(e.Value?.ToString(), out var n) ? n : 2;
    private void OnGenModeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _genForm.Mode = (HallAllocationMode)v;
    }
    private void OnGenCountChanged(ChangeEventArgs e) =>
        _genForm.Count = int.TryParse(e.Value?.ToString(), out var n) ? n : null;
    private void OnGenSpecChanged(ChangeEventArgs e) => _genForm.RowColumnSpec = e.Value?.ToString();
    private void OnGenCapacityChanged(ChangeEventArgs e) =>
        _genForm.Capacity = int.TryParse(e.Value?.ToString(), out var n) ? n : 2;
    private void OnAllocPurposeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _allocForm.Purpose = (HallPurpose)v;
    }
    private void OnAllocModeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _allocForm.Mode = (HallAllocationMode)v;
    }
    private void OnAllocCountChanged(ChangeEventArgs e) =>
        _allocForm.UnitCount = int.TryParse(e.Value?.ToString(), out var n) ? n : null;
    private void OnAllocSpecChanged(ChangeEventArgs e) => _allocForm.RowColumnSpec = e.Value?.ToString();
    private void OnAllocStartChanged(ChangeEventArgs e) => _allocStartText = e.Value?.ToString() ?? string.Empty;
    private void OnAllocEndChanged(ChangeEventArgs e) => _allocEndText = e.Value?.ToString() ?? string.Empty;

    private static string? Trimmed(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseUtc(string text, out DateTime value)
    {
        value = default;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            value = SaudiTime.FromSaudiWallClock(dt);
            return true;
        }
        return false;
    }

    // R10 (D-767) — the resx key for a hall purpose / allocation mode, so the
    // allocations grid renders the localized label instead of the raw enum name
    // (e.g. "RandomByCount"). Mirrors the option keys the pickers already use.
    private static string PurposeKey(HallPurpose purpose) => purpose switch
    {
        HallPurpose.General => "Admin.MeetingTables.Purpose.General",
        HallPurpose.Booth => "Admin.MeetingTables.Purpose.Booth",
        HallPurpose.Session => "Admin.MeetingTables.Purpose.Session",
        HallPurpose.Meeting => "Admin.MeetingTables.Purpose.Meeting",
        _ => "Admin.MeetingTables.Purpose.General",
    };

    private static string ModeKey(HallAllocationMode mode) => mode switch
    {
        HallAllocationMode.Whole => "Admin.MeetingTables.Mode.Whole",
        HallAllocationMode.RandomByCount => "Admin.MeetingTables.Mode.RandomByCount",
        HallAllocationMode.RowColumn => "Admin.MeetingTables.Mode.RowColumn",
        _ => "Admin.MeetingTables.Mode.Whole",
    };
}
