using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionSummariesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSessionSummaryRow> _rows = new();
    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSessionSummaryRow> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private SaveSessionSummaryRequest? _edit;
    private Guid _editSessionId;
    private string _editTitle = string.Empty;
    private string? _editAiModel;
    // A19 — the review state the editor opened on, plus the values it opened
    // with. Saving an EDITED summary clears the approval and takes it offline
    // (server side, D-472 + owner 2026-07-19), so the reviewer is warned first
    // instead of the محضر silently vanishing from the app.
    private bool _editWasPublished;
    private bool _editWasApprovedOrInReview;
    private SaveSessionSummaryRequest _editOriginal = new();
    private bool _confirmUnpublishOpen;
    // Slice D — read-only AI-transparency sources shown in the editor modal.
    private string _editSubtitle = string.Empty;
    private string _editSubtitleArabic = string.Empty;
    private string _editAiDraftArabic = string.Empty;
    private DateTime? _editAiDraftGeneratedAt;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        BuildPage();
        return Task.CompletedTask;
    }

    // The desk loads every active session in one read, so filter / sort / page
    // happen client-side over the in-memory rows.
    private void BuildPage()
    {
        IEnumerable<AdminSessionSummaryRow> q = _rows;
        if (_query.Filters.TryGetValue("session", out var f) && !string.IsNullOrWhiteSpace(f))
        {
            q = q.Where(r => r.SessionTitle.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        if (string.Equals(_query.Sort, "session", StringComparison.OrdinalIgnoreCase))
        {
            q = _query.SortDescending
                ? q.OrderByDescending(r => r.SessionTitle)
                : q.OrderBy(r => r.SessionTitle);
        }
        var filtered = q.ToList();
        var items = filtered.Skip(_query.Skip).Take(_query.Top).ToList();
        _page = GridPage<AdminSessionSummaryRow>.Of(items, filtered.Count, _query);
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — summaries are
    // drafted / edited / published from this desk's own actions, so there is no
    // import path. Rows are keyed by SessionId (the desk has no separate Id).
    private async Task OnExportAsync(IReadOnlyList<AdminSessionSummaryRow> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/session-summaries/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.SessionId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>(
                "simfAccount.getJson", "/account/api/admin/session-summaries");
            if (envelope is { Success: true, Data: not null })
            {
                _rows = envelope.Data.ToList();
                BuildPage();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private async Task GenerateAsync(Guid sessionId)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.postJson",
                $"/account/api/admin/session-summaries/{sessionId}/generate", new { });
            if (envelope is { Success: true, Data: not null })
            {
                _toast = new Toast("success", L["Admin.SessionSummaries.Generated"]);
                LoadEditor(envelope.Data);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task OpenEditorAsync(Guid sessionId)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.getJson", $"/account/api/admin/session-summaries/{sessionId}");
            if (envelope is { Success: true, Data: not null })
            {
                LoadEditor(envelope.Data);
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private void LoadEditor(AdminSessionSummaryDetail detail)
    {
        _editSessionId = detail.SessionId;
        _editTitle = detail.SessionTitle;
        _editAiModel = detail.AiModel;
        _editSubtitle = detail.Subtitle ?? string.Empty;
        _editSubtitleArabic = detail.SubtitleArabic ?? string.Empty;
        _editAiDraftArabic = detail.AiDraftFullTextArabic ?? string.Empty;
        _editAiDraftGeneratedAt = detail.AiDraftGeneratedAt;
        // A19 — remember what saving would cost before the reviewer starts typing.
        _editWasPublished = detail.IsPublished;
        _editWasApprovedOrInReview = detail.IsApproved || detail.IsInReview;
        _edit = FromDetail(detail);
        _editOriginal = FromDetail(detail);
    }

    private static SaveSessionSummaryRequest FromDetail(AdminSessionSummaryDetail detail) =>
        new()
        {
            KeyPoints = detail.KeyPoints,
            KeyPointsArabic = detail.KeyPointsArabic,
            Recommendations = detail.Recommendations,
            RecommendationsArabic = detail.RecommendationsArabic,
            Speakers = detail.Speakers,
            SpeakersArabic = detail.SpeakersArabic,
            FullText = detail.FullText,
            FullTextArabic = detail.FullTextArabic,
            // Item #35 — the optional team summary-video URL round-trips through
            // the same upsert as the content sections.
            SummaryVideoUrl = detail.SummaryVideoUrl,
        };

    private void CloseEditor()
    {
        _edit = null;
        _confirmUnpublishOpen = false;
    }

    // A19 — true when the reviewer actually changed something the server persists.
    // Mirrors AdminSessionSummaryService.ChangesPersistedContent, so the warning
    // fires exactly when the save would reset the review state.
    private bool HasUnsavedContentChange()
    {
        if (_edit is null) { return false; }
        return !string.Equals(_edit.KeyPoints, _editOriginal.KeyPoints, StringComparison.Ordinal)
            || !string.Equals(_edit.KeyPointsArabic, _editOriginal.KeyPointsArabic, StringComparison.Ordinal)
            || !string.Equals(_edit.Recommendations, _editOriginal.Recommendations, StringComparison.Ordinal)
            || !string.Equals(_edit.RecommendationsArabic, _editOriginal.RecommendationsArabic, StringComparison.Ordinal)
            || !string.Equals(_edit.Speakers, _editOriginal.Speakers, StringComparison.Ordinal)
            || !string.Equals(_edit.SpeakersArabic, _editOriginal.SpeakersArabic, StringComparison.Ordinal)
            || !string.Equals(_edit.FullText, _editOriginal.FullText, StringComparison.Ordinal)
            || !string.Equals(_edit.FullTextArabic, _editOriginal.FullTextArabic, StringComparison.Ordinal)
            || !string.Equals(_edit.SummaryVideoUrl, _editOriginal.SummaryVideoUrl, StringComparison.Ordinal);
    }

    // A19 — the Save button. When the edit would strip a published / approved
    // summary of its approval (and pull it out of the app), ask first and name
    // that consequence; otherwise save straight away.
    private Task OnSaveClickedAsync()
    {
        if ((_editWasPublished || _editWasApprovedOrInReview) && HasUnsavedContentChange())
        {
            _confirmUnpublishOpen = true;
            return Task.CompletedTask;
        }
        return SaveAsync();
    }

    private void CancelUnpublishConfirm() => _confirmUnpublishOpen = false;

    private Task ConfirmUnpublishSaveAsync()
    {
        _confirmUnpublishOpen = false;
        return SaveAsync();
    }

    /// <summary>The consequence spelled out on the A19 confirmation — a published
    /// summary disappears from the app; an approved / in-review one only loses
    /// its sign-off.</summary>
    private string UnpublishConfirmMessage =>
        _editWasPublished
            ? L["Admin.SessionSummaries.Confirm.UnpublishOnSave"]
            : L["Admin.SessionSummaries.Confirm.UnapproveOnSave"];

    private async Task SaveAsync()
    {
        if (_edit is null) { return; }
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/session-summaries/{_editSessionId}", _edit);
            if (envelope is { Success: true })
            {
                _edit = null;
                _toast = new Toast("success", L["Admin.SessionSummaries.Saved"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task SetPublishedAsync(Guid sessionId, bool publish)
    {
        _busy = true;
        _toast = null;
        try
        {
            var url = publish
                ? $"/account/api/admin/session-summaries/{sessionId}/publish"
                : $"/account/api/admin/session-summaries/{sessionId}/unpublish";
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.putJson", url, new { });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success",
                    publish ? L["Admin.SessionSummaries.Published"]
                            : L["Admin.SessionSummaries.Unpublished"]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private string SourceLabel(AdminSessionSummaryRow row) =>
        !row.HasSummary ? "—"
        : row.GeneratedByAi ? L["Admin.SessionSummaries.Source.Ai"]
        : L["Admin.SessionSummaries.Source.Manual"];

    // Slice D — the pristine AI-draft panel label, with the capture time rendered
    // on the Saudi wall clock (the CP's 12-hour convention) when one is recorded.
    private string AiDraftLabel =>
        _editAiDraftGeneratedAt is { } at
            ? $"{L["Admin.SessionSummaries.Field.AiDraft"]} · {at.FormatSaudi("dd-MM-yyyy hh:mm tt")}"
            : L["Admin.SessionSummaries.Field.AiDraft"];

    // D-472 (#9) — the team review/approval workflow actions. Each forwards a PUT
    // to the matching admin endpoint, toasts, and reloads the desk.
    private Task SubmitReviewAsync(Guid sessionId) =>
        TransitionAsync(sessionId, "submit-review", "Admin.SessionSummaries.Submitted");

    private Task ApproveSummaryAsync(Guid sessionId) =>
        TransitionAsync(sessionId, "approve", "Admin.SessionSummaries.Approved");

    private Task ReturnToDraftAsync(Guid sessionId) =>
        TransitionAsync(sessionId, "return-to-draft", "Admin.SessionSummaries.ReturnedToDraft");

    private async Task TransitionAsync(Guid sessionId, string action, string successKey)
    {
        _busy = true;
        _toast = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.putJson",
                $"/account/api/admin/session-summaries/{sessionId}/{action}", new { });
            if (envelope is { Success: true })
            {
                _toast = new Toast("success", L[successKey]);
                await LoadAsync();
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SessionSummaries.Fallback"]);
            }
        }
        finally { _busy = false; }
    }
}
