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

    private bool _respondOpen;
    // PII (requester email) is fetched on demand into the detail shape; list
    // rows do not carry email (the D-185 pattern).
    private AdminSpeakerMeetingRequestDetail? _respondTarget;
    private bool _loadingDetail;
    private MeetingRequestStatus _respondStatus = MeetingRequestStatus.Accepted;
    private string _respondNote = string.Empty;

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
        _respondStatus = MeetingRequestStatus.Accepted;
        _respondNote = string.Empty;
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

    private void OnRespondStatusChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var v))
        {
            _respondStatus = (MeetingRequestStatus)v;
        }
    }

    private void OnRespondNoteChanged(ChangeEventArgs e) =>
        _respondNote = e.Value?.ToString() ?? string.Empty;

    private async Task SendResponseAsync()
    {
        if (_respondTarget is null || _busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerMeetingRequestDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/speaker-meeting-requests/{_respondTarget.Id}/respond",
                new RespondToSpeakerMeetingRequestRequest
                {
                    Status = _respondStatus,
                    ResponseNote = string.IsNullOrWhiteSpace(_respondNote)
                        ? null : _respondNote.Trim(),
                });
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
