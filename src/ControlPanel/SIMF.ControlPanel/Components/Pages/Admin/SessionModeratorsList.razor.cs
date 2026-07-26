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
    // DEF-MOD-005 — the assign dialog picks from real option lists instead of two
    // free-text GUID boxes (a typo used to hand a moderation desk to whichever
    // account the GUID happened to name).
    private IReadOnlyList<SessionModeratorSessionOption> _sessionOptions =
        Array.Empty<SessionModeratorSessionOption>();
    private IReadOnlyList<SessionModeratorCandidate> _candidates =
        Array.Empty<SessionModeratorCandidate>();
    private SessionModeratorSessionOption? _newSession;
    private SessionModeratorCandidate? _newCandidate;
    private bool _optionsLoading;
    private bool _optionsError;

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

    private async Task OnAddAsync()
    {
        _addOpen = true;
        _newSession = null;
        _newCandidate = null;
        await LoadAssignOptionsAsync();
    }

    /// <summary>DEF-MOD-005 — loads the two pickers. Gated API-side by
    /// <c>SessionModerators.Assign</c>, the same permission as the write.</summary>
    private async Task LoadAssignOptionsAsync()
    {
        _optionsLoading = true;
        _optionsError = false;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SessionModeratorAssignOptions>>(
                "simfAccount.getJson",
                "/account/api/admin/session-moderators/assign-options");
            if (env is { Success: true, Data: not null })
            {
                _sessionOptions = env.Data.Sessions;
                _candidates = env.Data.Candidates;
            }
            else
            {
                _optionsError = true;
                _sessionOptions = Array.Empty<SessionModeratorSessionOption>();
                _candidates = Array.Empty<SessionModeratorCandidate>();
            }
        }
        finally { _optionsLoading = false; }
    }

    private async Task SubmitAssignAsync()
    {
        if (_busy) return;
        if (_newSession is null || _newCandidate is null)
        {
            _toast = new Toast("error", L["Admin.SessionModerators.PickBoth"]);
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
                    SessionId = _newSession.Id,
                    UserId = _newCandidate.UserId,
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

    private static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    private static string SessionLabel(AdminSessionModeratorRow row) =>
        IsArabic
            ? $"{row.SessionCode} — {row.SessionTitleArabic}"
            : $"{row.SessionCode} — {row.SessionTitle}";

    private static string SessionOptionLabel(SessionModeratorSessionOption option) =>
        IsArabic
            ? $"{option.Code} — {option.TitleArabic}"
            : $"{option.Code} — {option.Title}";

    private static string CandidateLabel(SessionModeratorCandidate candidate)
    {
        var type = IsArabic ? candidate.ProfileTypeNameArabic : candidate.ProfileTypeName;
        var email = string.IsNullOrWhiteSpace(candidate.Email) ? "—" : candidate.Email;
        return $"{candidate.DisplayName} ({email}) — {type}";
    }
}
