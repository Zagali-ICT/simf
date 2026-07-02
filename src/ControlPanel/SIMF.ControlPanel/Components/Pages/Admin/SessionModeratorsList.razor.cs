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

public partial class SessionModeratorsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSessionModeratorRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private bool _addOpen;
    private string _newSessionId = string.Empty;
    private string _newUserId = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — grants are managed
    // in place via assign/revoke; the row's UserId is the selectable id.
    private Task OnExportAsync(IReadOnlyList<AdminSessionModeratorRow> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/session-moderators/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.UserId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionModeratorRow>>>(
                "simfAccount.postJson", "/account/api/admin/session-moderators/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModerators.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OnAdd()
    {
        _addOpen = true;
        _newSessionId = string.Empty;
        _newUserId = string.Empty;
    }

    private async Task SubmitAssignAsync()
    {
        if (_busy) return;
        if (!Guid.TryParse(_newSessionId, out var sessionId)
            || !Guid.TryParse(_newUserId, out var userId))
        {
            _toast = new Toast("error", L["Admin.SessionModerators.LoadFailed"]);
            return;
        }
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSessionModeratorRow>>(
                "simfAccount.postJson", "/account/api/admin/session-moderators",
                new AssignSessionModeratorRequest
                {
                    SessionId = sessionId,
                    UserId = userId,
                });
            if (env is { Success: true })
            {
                _addOpen = false;
                _toast = new Toast("success", L["Admin.SessionModerators.Assigned"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModerators.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task RevokeAsync(AdminSessionModeratorRow row)
    {
        if (_busy) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/session-moderators/{row.SessionId}/{row.UserId}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SessionModerators.Revoked"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionModerators.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private static string SessionLabel(AdminSessionModeratorRow row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? $"{row.SessionCode} — {row.SessionTitleArabic}"
            : $"{row.SessionCode} — {row.SessionTitle}";
}
