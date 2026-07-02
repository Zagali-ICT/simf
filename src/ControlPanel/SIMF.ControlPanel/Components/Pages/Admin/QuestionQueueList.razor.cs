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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class QuestionQueueList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    // The full Pending queue as returned by the backend (oldest-first, capped at
    // 200). The grid pages / filters / sorts this in memory — the backend read
    // is a plain non-paged list, so there is no server GridQuery to extend.
    private List<SessionQuestionQueueRow> _rows = new();
    private GridQuery _query = new() { Top = 20 };
    private GridPage<SessionQuestionQueueRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private Guid? _escalateId;
    private string _escalateRole = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        // A sort / filter / page change re-projects the already-fetched list; no
        // round-trip — the queue was loaded whole in LoadAsync.
        _query = next;
        _page = BuildPage();
        await Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<SessionQuestionQueueRow>>>(
                "simfAccount.getJson", "/account/api/admin/questions/queue");
            if (envelope is { Success: true, Data: not null })
            {
                _rows = envelope.Data.ToList();
                _page = BuildPage();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.QuestionQueue.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Client-side projection of the in-memory queue onto the current GridQuery
    // (filters → sort → page). Filters are case-insensitive Contains, mirroring
    // the server-side grids; the default order preserves the backend's
    // oldest-first (CreatedAt) ordering.
    private GridPage<SessionQuestionQueueRow> BuildPage()
    {
        IEnumerable<SessionQuestionQueueRow> rows = _rows;

        foreach (var (column, raw) in _query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            rows = column.ToLowerInvariant() switch
            {
                "session" => rows.Where(r => Contains(r.SessionTitle, v)),
                "question" => rows.Where(r => Contains(r.QuestionText, v)),
                "submitter" => rows.Where(r => Contains(r.SubmittedByDisplayName, v)),
                "ai" => rows.Where(r => Contains(r.AiFilterVerdict, v)),
                _ => rows,
            };
        }

        rows = (_query.Sort?.ToLowerInvariant(), _query.SortDescending) switch
        {
            ("session", false) => rows.OrderBy(r => r.SessionTitle),
            ("session", true) => rows.OrderByDescending(r => r.SessionTitle),
            ("question", false) => rows.OrderBy(r => r.QuestionText),
            ("question", true) => rows.OrderByDescending(r => r.QuestionText),
            ("submitter", false) => rows.OrderBy(r => r.SubmittedByDisplayName),
            ("submitter", true) => rows.OrderByDescending(r => r.SubmittedByDisplayName),
            ("phase", false) => rows.OrderBy(r => r.Phase),
            ("phase", true) => rows.OrderByDescending(r => r.Phase),
            _ => rows.OrderBy(r => r.CreatedAt),
        };

        var materialised = rows.ToList();
        var total = materialised.Count;
        var skip = Math.Max(0, _query.Skip);
        var top = _query.Top is > 0 ? _query.Top : 20;
        var items = materialised.Skip(skip).Take(top).ToList();
        return GridPage<SessionQuestionQueueRow>.Of(items, total, _query);
    }

    private static bool Contains(string? value, string needle) =>
        value is not null
        && value.Contains(needle, StringComparison.OrdinalIgnoreCase);

    // D-356 — Excel export (selected rows, or the whole Pending queue). Direct
    // download via the generic /export proxy. Export only — questions are
    // audience-submitted and moderated in place (approve / hide / escalate); the
    // queue is not scoped by a parent picked on the page, so an empty selection
    // exports the full cross-session Pending queue the server already lists.
    private Task OnExportAsync(IReadOnlyList<SessionQuestionQueueRow> selected) =>
        JS.InvokeVoidAsync("simfAccount.downloadXlsx",
            "/account/api/admin/questions/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }).AsTask();

    private Task ApproveAsync(Guid id) =>
        ActAsync($"/account/api/admin/questions/{id}/approve", L["Admin.QuestionQueue.Approved"]);

    private Task HideAsync(Guid id) =>
        ActAsync($"/account/api/admin/questions/{id}/hide", L["Admin.QuestionQueue.Hidden"]);

    private void OpenEscalate(Guid id)
    {
        _escalateId = id;
        _escalateRole = string.Empty;
    }

    private async Task EscalateAsync()
    {
        if (_escalateId is not { } id) { return; }
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<SessionQuestionQueueRow>>(
                "simfAccount.putJson",
                $"/account/api/admin/questions/{id}/escalate",
                new EscalateQuestionRequest { Role = _escalateRole });
            if (envelope is { Success: true })
            {
                _escalateId = null;
                _toast = new Toast("success", L["Admin.QuestionQueue.Escalated"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.QuestionQueue.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task ActAsync(string url, string successMessage)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<SessionQuestionQueueRow>>(
                "simfAccount.putJson", url, new { });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", successMessage);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.QuestionQueue.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);
}
