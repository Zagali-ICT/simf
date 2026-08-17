using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
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

    // Excel export/import wired to the reusable CrudGridExcel component.
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

    // Append-only version history for one prompt, one page at a time
    // (POST /admin/ai/prompts/{id}/history/list, gated on AiPrompts.View). The
    // history gains a row on every edit and is never pruned, so the modal pages
    // through it rather than rendering the whole set. The request carries only
    // Skip/Top: the order is the server's default (newest version first), and the
    // modal offers no sort or filter, so it can never send an undeclared key.
    private bool _historyOpen;
    private string _historyKey = string.Empty;
    private Guid _historyPromptId;
    private GridQuery _historyQuery = new() { Top = HistoryPageSize };
    private GridPage<AdminAiPromptHistoryEntry> _historyPage = new();

    private const int HistoryPageSize = 20;

    private bool HistoryHasPrev => _historyPage.Skip > 0;

    private bool HistoryHasNext =>
        _historyPage.Skip + _historyPage.Items.Count < _historyPage.Total;

    private string HistoryPageLabel
    {
        get
        {
            var size = _historyPage.Top > 0 ? _historyPage.Top : HistoryPageSize;
            var current = (_historyPage.Skip / size) + 1;
            var total = Math.Max(1, (int)Math.Ceiling(_historyPage.Total / (double)size));
            return string.Format(L["Grid.Page"], current, total);
        }
    }

    private async Task OnHistoryAsync(AdminAiPromptSummary row)
    {
        _toast = null;
        _historyKey = row.Key;
        _historyPromptId = row.Id;
        _historyQuery = new GridQuery { Top = HistoryPageSize };
        _historyPage = new();
        await LoadHistoryAsync(openOnSuccess: true);
    }

    private async Task HistoryPrevAsync()
    {
        if (!HistoryHasPrev) return;
        _historyQuery = new GridQuery
        {
            Skip = Math.Max(0, _historyPage.Skip - HistoryPageSize),
            Top = HistoryPageSize,
        };
        await LoadHistoryAsync(openOnSuccess: false);
    }

    private async Task HistoryNextAsync()
    {
        if (!HistoryHasNext) return;
        _historyQuery = new GridQuery
        {
            Skip = _historyPage.Skip + HistoryPageSize,
            Top = HistoryPageSize,
        };
        await LoadHistoryAsync(openOnSuccess: false);
    }

    private async Task LoadHistoryAsync(bool openOnSuccess)
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAiPromptHistoryEntry>>>(
            "simfAccount.postJson",
            $"/account/api/admin/ai/prompts/{_historyPromptId}/history/list",
            _historyQuery);
        if (env is { Success: true, Data: not null })
        {
            _historyPage = env.Data;
            if (openOnSuccess) _historyOpen = true;
            return;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture() ?? L["Admin.AiPrompts.LoadFailed"]);
    }

    private void CloseHistory()
    {
        _historyOpen = false;
        _historyKey = string.Empty;
        _historyPromptId = Guid.Empty;
        _historyQuery = new GridQuery { Top = HistoryPageSize };
        _historyPage = new();
    }

    // Show a readable prefix of the (long) content-hash in the history table.
    private static string ShortHash(string hash) =>
        string.IsNullOrEmpty(hash) ? "—"
        : hash.Length <= 16 ? hash : hash[..16] + "…";
}
