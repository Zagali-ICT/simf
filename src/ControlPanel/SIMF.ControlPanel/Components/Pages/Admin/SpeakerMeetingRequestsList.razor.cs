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
using SIMF.Contracts.Programme;
using SIMF.Contracts.BusinessMeetings;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SpeakerMeetingRequestsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSpeakerMeetingRequestRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    // R10 (D-767) — a confirm dialog for the one-click, state-changing row actions
    // (Check-in flips Accepted→Done; Resend emails fresh links) so a mis-click cannot
    // fire them silently. The work runs from RunConfirmAsync only after the admin OKs.
    private bool _confirmOpen;
    private string _confirmTitle = string.Empty;
    private string _confirmMessage = string.Empty;
    private string _confirmLabel = string.Empty;
    private Func<Task>? _confirmAction;

    private bool _respondOpen;
    // PII (requester email) is fetched on demand into the detail shape; list
    // rows do not carry email (the D-185 pattern).
    private AdminSpeakerMeetingRequestDetail? _respondTarget;
    private bool _loadingDetail;
    private string _respondNote = string.Empty;

    // D-716 (item 7, GAP-2) — optional hall binding on Accept: pick a meeting hall,
    // one of its free slots, and (optionally) a table. Binding moves the request to
    // AwaitingSpeaker (double-opt-in). Leaving the hall unset keeps the legacy
    // straight-to-Accepted behaviour.
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
    // enum so a wire-int reorder can't drift (mirrors HallAvailabilityPage).
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

    // D-716 — a free hall slot as "2026-07-10 09:00 AM–09:30 AM" (Saudi time).
    private static string FormatSlot(HallAvailableSlot slot) =>
        $"{slot.Start.ToSaudi():dd-MM-yyyy hh:mm tt}–{slot.End.ToSaudi():hh:mm tt}";

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/speaker-meeting-requests/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerMeetingRequests.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — speaker meeting
    // requests are created from the app + responded to in the CP, so there is
    // no import path.
    private Task OnExportAsync(IReadOnlyList<AdminSpeakerMeetingRequestRow> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/speaker-meeting-requests/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    private async Task OnRespondAsync(AdminSpeakerMeetingRequestRow row)
    {
        // Open the modal with what the row carries (no email yet), then fetch
        // the detail (with email) in the background — one audited Viewed event
        // per click (D-185 pattern).
        _respondTarget = new AdminSpeakerMeetingRequestDetail(
            row.Id, row.SpeakerId, row.SpeakerName, row.SpeakerNameArabic,
            row.RequestedByUserId, row.RequesterName, RequesterEmail: null,
            row.Subject, row.Status, row.ResponseNote,
            row.CreatedAt, row.RespondedAt);
        _respondNote = string.Empty;
        _bindHallId = null;
        ClearBindSelection();
        _respondOpen = true;
        _loadingDetail = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                "simfAccount.getJson",
                $"/account/api/admin/speaker-meeting-requests/{row.Id}");
            if (env is { Success: true, Data: not null } && _respondOpen
                && _respondTarget?.Id == row.Id)
            {
                _respondTarget = env.Data;
            }
        }
        finally { _loadingDetail = false; }
    }

    // R-1 — re-send the speaker's Approve/Reject confirmation email for an
    // AwaitingSpeaker row (fresh 72h links). Quiet row action, no modal.
    private async Task OnResendAsync(AdminSpeakerMeetingRequestRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/speaker-meeting-requests/{row.Id}/resend-confirmation",
                new { });
            _toast = env is { Success: true }
                ? new Toast("success", L["Admin.SpeakerMeetingRequests.Resend.Done"])
                : new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerMeetingRequests.LoadFailed"]);
        }
        finally { _busy = false; }
    }

    private void OnRespondNoteChanged(ChangeEventArgs e) =>
        _respondNote = e.Value?.ToString() ?? string.Empty;

    // Bi-Meeting rework — an operator checks a confirmed (Accepted) meeting in at the
    // hall; the backend flips it to Done. Quiet row action (no modal).
    private async Task OnCheckInAsync(AdminSpeakerMeetingRequestRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                "simfAccount.postJson",
                $"/account/api/admin/speaker-meeting-requests/{row.Id}/check-in",
                new { });
            _toast = env is { Success: true }
                ? new Toast("success", L["Admin.Meetings.CheckIn.Done"])
                : new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerMeetingRequests.LoadFailed"]);
            if (env is { Success: true }) { await LoadAsync(); }
        }
        finally { _busy = false; }
    }

    // QA B20 — reopen a Rejected / Cancelled request back to Pending so a mistaken
    // decline or cancel is recoverable. Quiet row action behind the same confirm
    // dialog as Check-in / Resend, gated by the page's Manage permission.
    private async Task OnReopenAsync(AdminSpeakerMeetingRequestRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                "simfAccount.postJson",
                $"/account/api/admin/speaker-meeting-requests/{row.Id}/reopen",
                new { });
            _toast = env is { Success: true }
                ? new Toast("success", L["Admin.SpeakerMeetingRequests.Reopen.Done"])
                : new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerMeetingRequests.LoadFailed"]);
            if (env is { Success: true }) { await LoadAsync(); }
        }
        finally { _busy = false; }
    }

    // R10 (D-767) — open the confirm dialog for a one-click row action; the work runs
    // from RunConfirmAsync only after the admin confirms.
    private void ConfirmReopen(AdminSpeakerMeetingRequestRow row) =>
        AskConfirm(L["Admin.SpeakerMeetingRequests.Reopen"],
            L["Admin.SpeakerMeetingRequests.Reopen.ConfirmMsg"],
            L["Admin.SpeakerMeetingRequests.Reopen"], () => OnReopenAsync(row));

    private void ConfirmCheckIn(AdminSpeakerMeetingRequestRow row) =>
        AskConfirm(L["Admin.Meetings.CheckIn"], L["Admin.Meetings.CheckIn.ConfirmMsg"],
            L["Admin.Meetings.CheckIn"], () => OnCheckInAsync(row));

    private void ConfirmResend(AdminSpeakerMeetingRequestRow row) =>
        AskConfirm(L["Admin.SpeakerMeetingRequests.Resend"],
            L["Admin.SpeakerMeetingRequests.Resend.ConfirmMsg"],
            L["Admin.SpeakerMeetingRequests.Resend"], () => OnResendAsync(row));

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

    // D-716 — reset the hall-binding selection (the chosen hall is set separately;
    // this clears the slot/table choice + their loaded lists).
    private void ClearBindSelection()
    {
        _bindSlotIndex = -1;
        _bindTableId = null;
        _hallSlots = new();
        _hallTables = new();
    }

    // D-716 — pick a hall to bind on Accept; load its free slots + tables. The two
    // reads are independent, so they run concurrently (one modal-open round-trip).
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

    // Bi-Meeting rework — the 3-button model. Decline = Reject (justification
    // required). Approve = accept + bind the hall slot, verbal=false → the speaker's
    // own confirmation link is emailed. Confirm = accept + bind + verbal=true → booked
    // straight away (the admin already has the speaker's verbal confirmation).
    private Task DeclineAsync() =>
        SendRespondAsync(MeetingRequestStatus.Rejected, verbal: false, requireHall: false);

    private Task ApproveAsync() =>
        SendRespondAsync(MeetingRequestStatus.Accepted, verbal: false, requireHall: true);

    private Task ConfirmAsync() =>
        SendRespondAsync(MeetingRequestStatus.Accepted, verbal: true, requireHall: true);

    // QA B19 — with no HallAvailabilityWindow configured there is no free slot to bind,
    // so Approve and Confirm both refuse and Decline was the admin's only usable action.
    // The service has always supported accepting WITHOUT a hall (straight to Accepted, no
    // room booked), so offer it explicitly as its own clearly-labelled button.
    // VerbalConfirmed is only read when a hall IS bound, so it is left false here.
    private Task AcceptWithoutHallAsync() =>
        SendRespondAsync(MeetingRequestStatus.Accepted, verbal: false, requireHall: false);

    private async Task SendRespondAsync(
        MeetingRequestStatus status, bool verbal, bool requireHall)
    {
        if (_respondTarget is null || _busy) return;

        // A decline must carry a justification (owner rule: cancel-with-justification).
        if (status == MeetingRequestStatus.Rejected
            && string.IsNullOrWhiteSpace(_respondNote))
        {
            _toast = new Toast("error", L["Admin.Meetings.Decline.NoteRequired"]);
            return;
        }

        // Approve / Confirm bind the meeting to a free hall slot.
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
            var body = new RespondToSpeakerMeetingRequestRequest
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
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/speaker-meeting-requests/{_respondTarget.Id}/respond",
                body);
            if (env is { Success: true })
            {
                _respondOpen = false;
                _toast = new Toast("success", L["Admin.SpeakerMeetingRequests.Respond.Done"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerMeetingRequests.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
