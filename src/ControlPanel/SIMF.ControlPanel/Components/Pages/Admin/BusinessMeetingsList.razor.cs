using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Programme;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BusinessMeetingsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);
    private sealed record PendingParty(MeetingPartyKind Kind, Guid Id, string Name);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<BusinessMeetingRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // Cached pickers (bounded Top=500, mirrors BoothsList).
    private List<AdminHallSummary> _meetingHalls = new();
    private List<AdminExhibitorSummary> _companies = new();
    private List<AdminAttendeeSummary> _visitors = new();

    // Forum-day bounds for the datetime-local pickers (yyyy-MM-ddTHH:mm),
    // null when no programme days are seeded (no client bound; the server still
    // enforces the rule once days exist).
    private string? _forumMin;
    private string? _forumMax;

    // Schedule modal state.
    private bool _scheduleOpen;
    private Guid? _hallId;
    private List<MeetingTableRow> _tables = new();
    private ScheduleMeetingRequest _form = new();
    private string _startText = string.Empty;
    private string _endText = string.Empty;
    private readonly List<PendingParty> _participants = new();
    private MeetingPartyKind _addKind = MeetingPartyKind.Company;
    private Guid? _addCompanyId;
    private Guid? _addVisitorId;

    // Cancel modal state.
    private bool _cancelOpen;
    private BusinessMeetingRow? _cancelTarget;
    private string _cancelReason = string.Empty;

    // Detail modal state.
    private bool _detailOpen;
    private BusinessMeetingDetail? _detail;

    protected override async Task OnInitializedAsync()
    {
        // Flip the grid into its loading state from the very first paint:
        // OnInitializedAsync first yields inside LoadPickersAsync, so without this
        // the in-progress frame would render with _loading == false and the spinner
        // would never show on first load. LoadAsync's finally clears it.
        _loading = true;
        await LoadPickersAsync();
        await LoadAsync();
    }

    private async Task LoadPickersAsync()
    {
        var hallsEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list", new GridQuery { Top = 500 });
        if (hallsEnv is { Success: true, Data: not null })
        {
            // Meeting tables live in Meeting or General halls (AdminHallSummary.Purpose
            // is the int wire value; compare via the enum so a reorder can't drift).
            _meetingHalls = hallsEnv.Data.Items
                .Where(h => h.IsActive
                    && ((HallPurpose)h.Purpose) is HallPurpose.Meeting or HallPurpose.General)
                .OrderBy(h => h.Name).ToList();
        }

        var companiesEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminExhibitorSummary>>>(
            "simfAccount.postJson", "/account/api/admin/exhibitors/list", new GridQuery { Top = 500 });
        if (companiesEnv is { Success: true, Data: not null })
        {
            _companies = companiesEnv.Data.Items.Where(c => c.IsActive)
                .OrderBy(c => c.NameEn).ToList();
        }

        var visitorsEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminAttendeeSummary>>>(
            "simfAccount.postJson", "/account/api/admin/attendees/list", new GridQuery { Top = 500 });
        if (visitorsEnv is { Success: true, Data: not null })
        {
            _visitors = visitorsEnv.Data.Items.OrderBy(v => v.DisplayName).ToList();
        }

        await LoadForumWindowAsync();
    }

    // Read the forum-day window (MIN/MAX programme day) and translate it into
    // datetime-local min/max attributes spanning the whole day. A failed / empty read
    // leaves both null (no client bound); the backend enforces the rule on save.
    private async Task LoadForumWindowAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<ForumWindowResponse>>(
            "simfAccount.getJson", "/account/api/admin/programme/forum-window");
        if (env is { Success: true, Data: not null })
        {
            _forumMin = env.Data.MinDate is { } min
                ? min.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00"
                : null;
            _forumMax = env.Data.MaxDate is { } max
                ? max.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T23:59"
                : null;
        }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    // Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — scheduling and
    // cancelling stay on the page's bespoke modals. The row id is the grid key.
    private async Task OnExportAsync(IReadOnlyList<BusinessMeetingRow> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/business-meetings/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<BusinessMeetingRow>>>(
                "simfAccount.postJson", "/account/api/admin/business-meetings/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BusinessMeetings.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OnAddAsync()
    {
        _scheduleOpen = true;
        _toast = null;
        _hallId = null;
        _tables = new();
        _form = new ScheduleMeetingRequest { MeetingType = BusinessMeetingType.B2B };
        _startText = string.Empty;
        _endText = string.Empty;
        _participants.Clear();
        _addKind = MeetingPartyKind.Company;
        _addCompanyId = null;
        _addVisitorId = null;
    }

    private async Task OnHallChangedAsync(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _hallId = Guid.TryParse(raw, out var g) ? g : null;
        _form.MeetingTableId = Guid.Empty;
        _tables = new();
        if (_hallId is { } hid)
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<MeetingTableRow>>>(
                "simfAccount.postJson",
                $"/account/api/admin/halls/{hid}/meeting-tables/list",
                new { Skip = 0, Top = 500 });
            if (env is { Success: true, Data: not null })
            {
                _tables = env.Data.Items.ToList();
            }
        }
    }

    private void OnTableChanged(ChangeEventArgs e) =>
        _form.MeetingTableId = Guid.TryParse(e.Value?.ToString(), out var g) ? g : Guid.Empty;

    private void OnTypeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _form.MeetingType = (BusinessMeetingType)v;
    }

    private void OnStartChanged(ChangeEventArgs e) => _startText = e.Value?.ToString() ?? string.Empty;
    private void OnEndChanged(ChangeEventArgs e) => _endText = e.Value?.ToString() ?? string.Empty;
    private void OnNotesChanged(ChangeEventArgs e) => _form.Notes = e.Value?.ToString();

    private void OnAddKindChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v)) _addKind = (MeetingPartyKind)v;
    }

    private void OnAddCompanyChanged(ChangeEventArgs e) =>
        _addCompanyId = Guid.TryParse(e.Value?.ToString(), out var g) ? g : null;

    private void OnAddVisitorChanged(ChangeEventArgs e) =>
        _addVisitorId = Guid.TryParse(e.Value?.ToString(), out var g) ? g : null;

    private void AddParticipant()
    {
        if (_addKind == MeetingPartyKind.Company && _addCompanyId is { } cid)
        {
            if (_participants.Any(p => p.Kind == MeetingPartyKind.Company && p.Id == cid)) return;
            var c = _companies.FirstOrDefault(x => x.Id == cid);
            _participants.Add(new PendingParty(MeetingPartyKind.Company, cid, c?.NameEn ?? cid.ToString()));
            _addCompanyId = null;
        }
        else if (_addKind == MeetingPartyKind.Visitor && _addVisitorId is { } vid)
        {
            if (_participants.Any(p => p.Kind == MeetingPartyKind.Visitor && p.Id == vid)) return;
            var v = _visitors.FirstOrDefault(x => x.UserId == vid);
            _participants.Add(new PendingParty(MeetingPartyKind.Visitor, vid, v?.DisplayName ?? vid.ToString()));
            _addVisitorId = null;
        }
    }

    private void RemoveParticipant(PendingParty p) => _participants.Remove(p);

    private async Task SaveAsync()
    {
        if (_busy) return;
        if (_form.MeetingTableId == Guid.Empty)
        {
            _toast = new Toast("error", L["Admin.BusinessMeetings.Validation.Table"]);
            return;
        }
        if (!TryParseSaudiWallClock(_startText, out var start) || !TryParseSaudiWallClock(_endText, out var end) || end <= start)
        {
            _toast = new Toast("error", L["Admin.BusinessMeetings.Validation.Slot"]);
            return;
        }
        if (_participants.Count < 2)
        {
            _toast = new Toast("error", L["Admin.BusinessMeetings.Validation.Participants"]);
            return;
        }
        _busy = true;
        _toast = null;
        try
        {
            _form.Start = start;
            _form.End = end;
            _form.Participants = _participants.Select(p => new ScheduleMeetingParticipant
            {
                Kind = p.Kind,
                CompanyId = p.Kind == MeetingPartyKind.Company ? p.Id : null,
                VisitorUserId = p.Kind == MeetingPartyKind.Visitor ? p.Id : null,
            }).ToList();

            var env = await JS.InvokeAsync<ApiResult<BusinessMeetingScheduled>>(
                "simfAccount.postJson", "/account/api/admin/business-meetings", _form);
            if (env is { Success: true })
            {
                _scheduleOpen = false;
                _toast = new Toast("success", L["Admin.BusinessMeetings.Scheduled"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BusinessMeetings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task OnViewAsync(BusinessMeetingRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<BusinessMeetingDetail>>(
                "simfAccount.getJson", $"/account/api/admin/business-meetings/{row.Id}");
            if (env is { Success: true, Data: not null })
            {
                _detail = env.Data;
                _detailOpen = true;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BusinessMeetings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OnCancel(BusinessMeetingRow row)
    {
        _cancelTarget = row;
        _cancelReason = string.Empty;
        _cancelOpen = true;
        _toast = null;
    }

    private void OnCancelReasonChanged(ChangeEventArgs e) =>
        _cancelReason = e.Value?.ToString() ?? string.Empty;

    private async Task ConfirmCancelAsync()
    {
        if (_busy || _cancelTarget is null) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/business-meetings/{_cancelTarget.Id}/cancel",
                new CancelMeetingRequest
                {
                    Reason = string.IsNullOrWhiteSpace(_cancelReason) ? null : _cancelReason.Trim(),
                });
            if (env is { Success: true })
            {
                _cancelOpen = false;
                _toast = new Toast("success", L["Admin.BusinessMeetings.Cancelled"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.BusinessMeetings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    // datetime-local carries no timezone, and under D-813 it does not need one:
    // what the admin types IS what gets stored. AssumeUniversal + AdjustToUniversal
    // is the parse that leaves a naked "2026-11-23T09:00" at 09:00 regardless of
    // the server's own timezone; FromSaudiWallClock then only stamps the Kind.
    // Do not "fix" this into a local-time parse — that would make the stored value
    // depend on where the Control Panel happens to be running.
    private static bool TryParseSaudiWallClock(string text, out DateTime value)
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
}
