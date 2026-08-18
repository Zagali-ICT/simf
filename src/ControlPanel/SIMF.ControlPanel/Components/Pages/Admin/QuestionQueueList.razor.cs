using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Sessions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class QuestionQueueList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

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
        _query = next;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<SessionQuestionQueueRow>>>(
                "simfAccount.postJson", "/account/api/admin/questions/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
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

    // Excel export (selected rows, or the whole Pending queue). Direct
    // download via the generic /export proxy. Export only — questions are
    // audience-submitted and moderated in place (approve / hide / escalate); the
    // queue is not scoped by a parent picked on the page, so an empty selection
    // exports the full cross-session Pending queue the server already lists.
    private async Task OnExportAsync(IReadOnlyList<SessionQuestionQueueRow> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/questions/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

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
