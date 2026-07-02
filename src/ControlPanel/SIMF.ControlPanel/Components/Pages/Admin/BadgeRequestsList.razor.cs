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
using SIMF.Contracts.Requests;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BadgeRequestsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminBadgeUpdateRequestRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private bool _respondOpen;
    private AdminBadgeUpdateRequestDetail? _respondTarget;
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminBadgeUpdateRequestRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/badge-requests/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.BadgeRequests.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnRespondAsync(AdminBadgeUpdateRequestRow row)
    {
        _respondTarget = new AdminBadgeUpdateRequestDetail(
            row.Id, row.RequestedByUserId, row.RequesterName, RequesterEmail: null,
            row.RequestedJobTitle, row.CurrentJobTitle, Note: null,
            row.Status, row.ResponseNote, row.CreatedAt, row.RespondedAt);
        _respondStatus = MeetingRequestStatus.Accepted;
        _respondNote = string.Empty;
        _respondOpen = true;
        _loadingDetail = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminBadgeUpdateRequestDetail>>(
                "simfAccount.getJson",
                $"/account/api/admin/badge-requests/{row.Id}");
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
            var env = await JS.InvokeAsync<ApiResult<AdminBadgeUpdateRequestDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/badge-requests/{_respondTarget.Id}/respond",
                new RespondToBadgeUpdateRequestRequest
                {
                    Status = _respondStatus,
                    ResponseNote = string.IsNullOrWhiteSpace(_respondNote)
                        ? null : _respondNote.Trim(),
                });
            if (env is { Success: true })
            {
                _respondOpen = false;
                _toast = new Toast("success", L["Admin.BadgeRequests.Respond.Done"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.BadgeRequests.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }
}
