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
using SIMF.Contracts.Ai;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiPromptsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "ai-prompts";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminAiPromptSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminAiPromptDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private bool _testOpen;
    private Guid _testTargetId;
    private string _testTargetKey = string.Empty;
    private string _testInputsRaw = string.Empty;
    private AiCallResult? _testResult;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.AiPrompts.Edit.Title"]
            : L["Admin.AiPrompts.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.AiPrompts.Delete.Title"]
            : L["Admin.AiPrompts.Details.Title"],
        _ => string.Empty,
    };

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
        await LoadAsync();
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

    // Hybrid hosting visibility is shared with the AI services console — see
    // AiHosting (CP-side derivation from provider + feature, no schema).

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAiPromptSummary>>>(
                "simfAccount.postJson", "/account/api/admin/ai/prompts/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiPrompts.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private async Task OnEditAsync(AdminAiPromptSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminAiPromptSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminAiPromptSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (the grid carries a
    // summary that lacks SystemPrompt / UserPromptTemplate / Description).
    // Returns null and surfaces a toast on failure.
    private async Task<AdminAiPromptDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminAiPromptDetail>>(
            "simfAccount.getJson", $"/account/api/admin/ai/prompts/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.AiPrompts.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminAiPromptSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminAiPromptDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.AiPrompts.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminAiPromptDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.AiPrompts.Deleted"]);
        await LoadAsync();
    }

    private void OnTestAsync(AdminAiPromptSummary row)
    {
        _testOpen = true;
        _testTargetId = row.Id;
        _testTargetKey = row.Key;
        _testInputsRaw = string.Empty;
        _testResult = null;
    }

    private void OnTestModalClose()
    {
        _testOpen = false;
        _testTargetId = Guid.Empty;
        _testTargetKey = string.Empty;
        _testInputsRaw = string.Empty;
        _testResult = null;
    }

    private async Task RunTestAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var inputs = new Dictionary<string, string>();
            foreach (var line in (_testInputsRaw ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                inputs[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
            var env = await JS.InvokeAsync<ApiResult<AiCallResult>>(
                "simfAccount.postJson",
                $"/account/api/admin/ai/prompts/{_testTargetId}/test",
                new TestAiPromptRequest { Inputs = inputs });
            if (env is { Success: true, Data: not null })
            {
                _testResult = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiPrompts.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private void OnTestInputsChanged(ChangeEventArgs e) =>
        _testInputsRaw = e.Value?.ToString() ?? string.Empty;

    // D-188 — append-only version history for one prompt, reusing the existing
    // GET /admin/ai/prompts/{id}/history endpoint (gated on AiPrompts.View).
    private bool _historyOpen;
    private string _historyKey = string.Empty;
    private IReadOnlyList<AdminAiPromptHistoryEntry> _history =
        Array.Empty<AdminAiPromptHistoryEntry>();

    private async Task OnHistoryAsync(AdminAiPromptSummary row)
    {
        _toast = null;
        _historyKey = row.Key;
        _history = Array.Empty<AdminAiPromptHistoryEntry>();
        var env = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminAiPromptHistoryEntry>>>(
            "simfAccount.getJson", $"/account/api/admin/ai/prompts/{row.Id}/history");
        if (env is { Success: true, Data: not null })
        {
            _history = env.Data;
            _historyOpen = true;
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.AiPrompts.LoadFailed"]);
        }
    }

    private void CloseHistory()
    {
        _historyOpen = false;
        _historyKey = string.Empty;
        _history = Array.Empty<AdminAiPromptHistoryEntry>();
    }

    // Show a readable prefix of the (long) content-hash in the history table.
    private static string ShortHash(string hash) =>
        string.IsNullOrEmpty(hash) ? "—"
        : hash.Length <= 16 ? hash : hash[..16] + "…";
}
