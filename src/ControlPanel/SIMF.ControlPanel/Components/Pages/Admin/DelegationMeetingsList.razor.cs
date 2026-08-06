using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class DelegationMeetingsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminDelegationMeetingRequestRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // A confirm dialog for the one-click Check-in row action (flips
    // Accepted→Done) so a mis-click cannot fire it silently.
    private bool _confirmOpen;
    private string _confirmTitle = string.Empty;
    private string _confirmMessage = string.Empty;
    private string _confirmLabel = string.Empty;
    private Func<Task>? _confirmAction;

    private bool _respondOpen;
    // PII (requester email) is fetched on demand into the detail shape; list
    // rows do not carry email.
    private AdminDelegationMeetingRequestDetail? _respondTarget;
    private bool _loadingDetail;
    private string _respondNote = string.Empty;

    // Bi-Meeting rework — hall binding on Approve/Confirm: pick a meeting hall, one of
    // its free slots, and (optionally) a table. Mirrors the speaker respond modal.
    private List<AdminHallSummary> _halls = new();
    private List<HallAvailableSlot> _hallSlots = new();
    private List<MeetingTableRow> _hallTables = new();
    private Guid? _bindHallId;
    private int _bindSlotIndex = -1;
    private Guid? _bindTableId;
    private bool _loadingSlots;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await LoadHallsAsync();
    }

    // Business meetings are hosted in Meeting (or General) halls — compare via the
    // enum so a wire-int reorder can't drift (mirrors the speaker desk).
    private async Task LoadHallsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _halls = env.Data.Items
                .Where(h => h.IsActive
                    && ((HallPurpose)h.Purpose) is HallPurpose.Meeting or HallPurpose.General)
                .OrderBy(h => h.Name).ToList();
        }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // A free hall slot as "2026-07-10 09:00–09:30".
    private static string FormatSlot(HallAvailableSlot slot) =>
        $"{slot.Start:dd-MM-yyyy hh:mm tt}–{slot.End:hh:mm tt}";

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminDelegationMeetingRequestRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/delegation-meeting-requests/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.DelegationMeetings.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnRespondAsync(AdminDelegationMeetingRequestRow row)
    {
        // Open the modal with what the row carries (no email yet), then fetch
        // the detail (with email) in the background — one audited Viewed event
        // per click.
        _respondTarget = new AdminDelegationMeetingRequestDetail(
            row.Id, row.RequestingCountry, row.TargetCountry,
            row.RequestedByUserId, RequesterEmail: null,
            row.AttendeeCount, row.Subject, row.Status,
            row.SlotStart, SlotEnd: null, row.ResponseNote,
            row.CreatedAt, row.RespondedAt);
        _respondNote = string.Empty;
        _bindHallId = null;
        ClearBindSelection();
        _respondOpen = true;
        _loadingDetail = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminDelegationMeetingRequestDetail>>(
                "simfAccount.getJson",
                $"/account/api/admin/delegation-meeting-requests/{row.Id}");
            if (env is { Success: true, Data: not null } && _respondOpen
                && _respondTarget?.Id == row.Id)
            {
                _respondTarget = env.Data;
            }
        }
        finally { _loadingDetail = false; }
    }

    private void OnRespondNoteChanged(ChangeEventArgs e) =>
        _respondNote = e.Value?.ToString() ?? string.Empty;

    // Reset the hall-binding selection (the chosen hall is set separately; this clears
    // the slot/table choice + their loaded lists).
    private void ClearBindSelection()
    {
        _bindSlotIndex = -1;
        _bindTableId = null;
        _hallSlots = new();
        _hallTables = new();
    }

    // Pick a hall to bind on Approve/Confirm; load its free slots + tables (the two
    // reads are independent, so they run concurrently).
    private async Task OnBindHallChangedAsync(ChangeEventArgs e)
    {
        _bindHallId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        ClearBindSelection();
        if (_bindHallId is not { } hallId) return;

        _loadingSlots = true;
        try
        {
            var slotsTask = JS.InvokeAsync<ApiResult<IReadOnlyList<HallAvailableSlot>>>(
                "simfAccount.getJson",
                $"/account/api/admin/halls/{hallId}/available-slots").AsTask();
            var tablesTask = JS.InvokeAsync<ApiResult<GridPage<MeetingTableRow>>>(
                "simfAccount.postJson",
                $"/account/api/admin/halls/{hallId}/meeting-tables/list",
                new GridQuery { Top = 500 }).AsTask();
            await Task.WhenAll(slotsTask, tablesTask);

            if (slotsTask.Result is { Success: true, Data: not null } slotsEnv)
            {
                _hallSlots = slotsEnv.Data.ToList();
            }
            if (tablesTask.Result is { Success: true, Data: not null } tablesEnv)
            {
                _hallTables = tablesEnv.Data.Items.Where(t => t.IsActive).ToList();
            }
        }
        finally { _loadingSlots = false; }
    }

    private void OnBindSlotChanged(ChangeEventArgs e) =>
        _bindSlotIndex = int.TryParse(e.Value?.ToString(), out var i) ? i : -1;

    private void OnBindTableChanged(ChangeEventArgs e) =>
        _bindTableId = Guid.TryParse(e.Value?.ToString(), out var g) ? g : null;

    // Bi-Meeting rework — an operator checks a confirmed (Accepted) meeting in → Done.
    // Quiet row action (no modal).
    private async Task OnCheckInAsync(AdminDelegationMeetingRequestRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminDelegationMeetingRequestDetail>>(
                "simfAccount.postJson",
                $"/account/api/admin/delegation-meeting-requests/{row.Id}/check-in",
                new { });
            _toast = env is { Success: true }
                ? new Toast("success", L["Admin.Meetings.CheckIn.Done"])
                : new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.DelegationMeetings.LoadFailed"]);
            if (env is { Success: true }) { await LoadAsync(); }
        }
        finally { _busy = false; }
    }

    // Guard the one-click Check-in behind a confirm dialog.
    private void ConfirmCheckIn(AdminDelegationMeetingRequestRow row) =>
        AskConfirm(L["Admin.Meetings.CheckIn"], L["Admin.Meetings.CheckIn.ConfirmMsg"],
            L["Admin.Meetings.CheckIn"], () => OnCheckInAsync(row));

    private void AskConfirm(string title, string message, string confirmLabel, Func<Task> action)
    {
        _confirmTitle = title;
        _confirmMessage = message;
        _confirmLabel = confirmLabel;
        _confirmAction = action;
        _confirmOpen = true;
        _toast = null;
    }

    private async Task RunConfirmAsync()
    {
        var action = _confirmAction;
        _confirmOpen = false;
        _confirmAction = null;
        if (action is not null)
        {
            await action();
        }
    }

    private void CancelConfirm() => _confirmOpen = false;

    // Bi-Meeting rework — the unified 3-button model. Decline/Cancel = Reject
    // (justification required), releasing any held hall. Approve = accept + bind a
    // hall slot, verbal=false → the other delegation is notified to confirm. Confirm
    // from Pending = accept + bind + verbal=true (booked straight away); Confirm from
    // AwaitingSpeaker keeps the already-bound hall (no re-pick) and finalises it.
    private Task DeclineAsync() =>
        SendRespondAsync(MeetingRequestStatus.Rejected, verbal: false, requireHall: false);

    private Task ApproveAsync() =>
        SendRespondAsync(MeetingRequestStatus.Accepted, verbal: false, requireHall: true);

    private Task ConfirmAsync()
    {
        // Confirm binds a hall only when finalising a fresh (Pending) request; an
        // already-Approved (AwaitingSpeaker) request keeps its bound slot.
        var fromPending = _respondTarget?.Status == MeetingRequestStatus.Pending;
        return SendRespondAsync(
            MeetingRequestStatus.Accepted, verbal: true, requireHall: fromPending);
    }

    private async Task SendRespondAsync(
        MeetingRequestStatus status, bool verbal, bool requireHall)
    {
        if (_respondTarget is null || _busy) return;

        // A decline / cancel must carry a justification (owner rule).
        if (status == MeetingRequestStatus.Rejected
            && string.IsNullOrWhiteSpace(_respondNote))
        {
            _toast = new Toast("error", L["Admin.Meetings.Decline.NoteRequired"]);
            return;
        }

        var haveSlot = _bindHallId is not null
            && _bindSlotIndex >= 0 && _bindSlotIndex < _hallSlots.Count;
        if (requireHall && !haveSlot)
        {
            _toast = new Toast("error", L["Admin.Meetings.Bind.HallSlotRequired"]);
            return;
        }

        _busy = true;
        _toast = null;
        try
        {
            var body = new RespondToDelegationMeetingRequestRequest
            {
                Status = status,
                VerbalConfirmed = verbal,
                ResponseNote = string.IsNullOrWhiteSpace(_respondNote)
                    ? null : _respondNote.Trim(),
            };
            if (requireHall && haveSlot)
            {
                var slot = _hallSlots[_bindSlotIndex];
                body.HallId = _bindHallId;
                body.MeetingTableId = _bindTableId;
                body.SlotStart = slot.Start;
                body.SlotEnd = slot.End;
            }
            var env = await JS.InvokeAsync<ApiResult<AdminDelegationMeetingRequestDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/delegation-meeting-requests/{_respondTarget.Id}/respond",
                body);
            if (env is { Success: true })
            {
                _respondOpen = false;
                _toast = new Toast("success", L["Admin.DelegationMeetings.Respond.Done"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.DelegationMeetings.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
