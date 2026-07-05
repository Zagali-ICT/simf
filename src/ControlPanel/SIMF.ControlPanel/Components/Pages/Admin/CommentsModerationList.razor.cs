using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Sessions;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class CommentsModerationList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummary> _sessions = new();
    private Guid? _selectedSessionId;
    private GridQuery _query = new() { Top = 20 };
    private GridPage<SessionCommentModerationRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadSessionsAsync();

    private async Task LoadSessionsAsync()
    {
        // The session picker is populated from the already-existing admin
        // sessions BFF passthrough. The list is bounded (one event's
        // worth of sessions) so a single Top=200 round-trip is fine.
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.postJson", "/account/api/admin/sessions/list",
                new GridQuery { Top = 200 });
            if (env is { Success: true, Data: not null })
            {
                _sessions = env.Data.Items.Where(s => s.IsActive)
                    .OrderBy(s => s.Code).ToList();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Comments.SessionsLoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task OnSessionChangedAsync(ChangeEventArgs e)
    {
        // Clear any stale toast so a message from session A does not
        // follow the admin to session B.
        _toast = null;
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            _selectedSessionId = id;
            // Reset paging/sort/filter when switching sessions.
            _query = new GridQuery { Top = 20 };
            await LoadCommentsAsync();
        }
        else
        {
            _selectedSessionId = null;
            _page = new GridPage<SessionCommentModerationRow>();
        }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadCommentsAsync();
    }

    private async Task LoadCommentsAsync()
    {
        if (_selectedSessionId is null) return;
        _loading = true;
        try
        {
            // The grid surfaces a per-column filter on the comment body; the
            // backend route filters the body through its free-text `Search`
            // field (`query.Search`), so the body filter is mapped there. The
            // `created` column is the only server-honoured sort key.
            _query.Filters.TryGetValue("body", out var bodyFilter);
            var env = await JS.InvokeAsync<ApiResult<GridPage<SessionCommentModerationRow>>>(
                "simfAccount.postJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/comments/list",
                new ListSessionCommentsBody
                {
                    Skip = _query.Skip,
                    Top = _query.Top,
                    Search = string.IsNullOrWhiteSpace(bodyFilter) ? null : bodyFilter,
                    Sort = _query.Sort,
                    SortDescending = _query.SortDescending,
                });
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Comments.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task SetStatusAsync(SessionCommentModerationRow row, SessionCommentStatus status)
    {
        if (_busy || _selectedSessionId is null) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SessionCommentModerationRow>>(
                "simfAccount.putJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/comments/{row.Id}/status",
                new SetSessionCommentStatusRequest { Status = status });
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.Comments.StatusSaved"]);
                await LoadCommentsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Comments.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task OnDeleteAsync(SessionCommentModerationRow row)
    {
        if (_busy || _selectedSessionId is null) return;
        // Confirm destructive actions — a soft-delete pulls the comment
        // from the moderation desk and the public feed immediately, and
        // there is no undo button today.
        var confirmed = await JS.InvokeAsync<bool>(
            "confirm", L["Admin.Comments.Delete.Confirm"].Value);
        if (!confirmed) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/sessions/{_selectedSessionId}/comments/{row.Id}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.Comments.Deleted"]);
                await LoadCommentsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Comments.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    // D-356 — Excel export (selected rows, or the whole picked-session set).
    // Direct download via the generic /export proxy. The moderation list is
    // session-scoped, so the picked session id rides in the query's filters
    // (the export endpoint reads Filters["sessionId"]); the export covers
    // every status, matching the desk's default view. Export only — comments
    // are submitted by the audience, so there is no import path.
    private Task OnExportAsync(IReadOnlyList<SessionCommentModerationRow> selected)
    {
        if (_selectedSessionId is null) return Task.CompletedTask;
        // The on-screen body filter maps to the service's free-text Search
        // (the comment body is the only searchable column), exactly as
        // LoadCommentsAsync does, so the export honours the filtered set.
        _query.Filters.TryGetValue("body", out var bodyFilter);
        var query = new GridQuery
        {
            Top = _query.Top,
            Sort = _query.Sort,
            SortDescending = _query.SortDescending,
            Search = string.IsNullOrWhiteSpace(bodyFilter) ? null : bodyFilter,
            Filters = new Dictionary<string, string> { ["sessionId"] = _selectedSessionId.Value.ToString() },
        };
        return JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/comments-moderation/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = query,
            }).AsTask();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Comments.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // The comments-list route carries the paging fields in the body
    // (the backend route composes Skip/Top/Search/Sort itself rather
    // than binding GridQuery, which is sealed). This mirrors those names.
    private sealed class ListSessionCommentsBody
    {
        public SessionCommentStatus? Status { get; set; }
        public int Skip { get; set; }
        public int Top { get; set; } = 25;
        public string? Search { get; set; }
        public string? Sort { get; set; }
        public bool SortDescending { get; set; } = true;
    }
}
