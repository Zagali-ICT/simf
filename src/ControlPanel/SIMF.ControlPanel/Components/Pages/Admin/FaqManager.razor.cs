using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class FaqManager
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _groupQuery = new() { Top = 20 };
    private GridPage<AdminFaqGroupSummary> _groupPage = new();
    private bool _groupLoading;

    private AdminFaqGroupSummary? _selectedGroup;
    private GridQuery _entryQuery = new() { Top = 20 };
    private GridPage<AdminFaqEntrySummary> _entryPage = new();
    private bool _entryLoading;

    private bool _busy;
    private Toast? _toast;

    /// <summary>The in-dialog error. Separate from <c>_toast</c> because the
    /// page-level alert renders under the modal backdrop and is invisible while
    /// a modal is open.</summary>
    private string? _error;

    // Group modal state.
    private bool _groupModalOpen;
    private Guid? _groupEditId;
    private string _groupNameEn = string.Empty;
    private string _groupNameAr = string.Empty;
    private string _groupOrder = "0";
    private bool _groupActive = true;

    /// <summary>The entry being read. The grid already holds the whole
    /// summary, including the answer text it has no column for, so Details opens
    /// straight from the row with no second fetch and no permission of its own.</summary>
    private AdminFaqEntrySummary? _entryDetails;

    private void OpenEntryDetails(AdminFaqEntrySummary entry) => _entryDetails = entry;

    // Entry modal state.
    private bool _entryModalOpen;
    private Guid? _entryEditId;
    private string _entryQuestionEn = string.Empty;
    private string _entryQuestionAr = string.Empty;
    private string _entryAnswerEn = string.Empty;
    private string _entryAnswerAr = string.Empty;
    private string _entryOrder = "0";
    private bool _entryActive = true;

    protected override async Task OnInitializedAsync() => await LoadGroupsAsync();

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    // -- Groups grid --
    private async Task OnGroupQueryChangedAsync(GridQuery next)
    {
        _groupQuery = next;
        await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        _groupLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminFaqGroupSummary>>>(
                "simfAccount.postJson", "/account/api/admin/faq/groups/list", _groupQuery);
            if (env is { Success: true, Data: not null })
            {
                _groupPage = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
            }
        }
        finally { _groupLoading = false; }
    }

    // -- Entries grid --
    private async Task SelectGroupAsync(AdminFaqGroupSummary group)
    {
        _selectedGroup = group;
        _entryQuery = new GridQuery { Top = 20 };
        await LoadEntriesAsync();
    }

    private async Task OnEntryQueryChangedAsync(GridQuery next)
    {
        _entryQuery = next;
        await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        if (_selectedGroup is null) return;
        _entryLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminFaqEntrySummary>>>(
                "simfAccount.postJson",
                $"/account/api/admin/faq/groups/{_selectedGroup.Id}/entries/list", _entryQuery);
            if (env is { Success: true, Data: not null })
            {
                _entryPage = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
            }
        }
        finally { _entryLoading = false; }
    }

    // -- Group modal --
    private void OpenAddGroup()
    {
        _groupEditId = null;
        _error = null;
        _groupNameEn = _groupNameAr = string.Empty;
        _groupOrder = "0";
        _groupActive = true;
        _groupModalOpen = true;
    }

    private void OpenEditGroup(AdminFaqGroupSummary g)
    {
        _error = null;
        _groupEditId = g.Id;
        _groupNameEn = g.NameEn;
        _groupNameAr = g.NameAr;
        _groupOrder = g.DisplayOrder.ToString();
        _groupActive = g.IsActive;
        _groupModalOpen = true;
    }

    private void CloseGroupModal() => _groupModalOpen = false;

    private async Task SaveGroupAsync()
    {
        if (_busy) return;
        _error = null;
        if (string.IsNullOrWhiteSpace(_groupNameEn) || string.IsNullOrWhiteSpace(_groupNameAr))
        {
            _error = L["Admin.Faq.Group.Required"];
            return;
        }
        _busy = true;
        _toast = null;
        try
        {
            int.TryParse(_groupOrder, out var order);
            ApiResult<AdminFaqGroupSummary>? env;
            if (_groupEditId is null)
            {
                env = await JS.InvokeAsync<ApiResult<AdminFaqGroupSummary>>(
                    "simfAccount.postJson", "/account/api/admin/faq/groups",
                    new CreateFaqGroupRequest { NameEn = _groupNameEn, NameAr = _groupNameAr, DisplayOrder = order });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminFaqGroupSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/faq/groups/{_groupEditId}",
                    new UpdateFaqGroupRequest { NameEn = _groupNameEn, NameAr = _groupNameAr, DisplayOrder = order, IsActive = _groupActive });
            }
            if (env is { Success: true })
            {
                _groupModalOpen = false;
                _toast = new Toast("success", L["Admin.Faq.Saved"]);
                await LoadGroupsAsync();
            }
            else
            {
                // Dialog still open — report in it, not behind it.
                _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    private async Task DeactivateGroupAsync(AdminFaqGroupSummary g)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/admin/faq/groups/{g.Id}");
        if (env is { Success: true })
        {
            _toast = new Toast("success", L["Admin.Faq.Deactivated"]);
            if (_selectedGroup?.Id == g.Id) { _selectedGroup = null; }
            await LoadGroupsAsync();
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
        }
    }

    // -- Entry modal --
    private void OpenAddEntry()
    {
        _entryEditId = null;
        _error = null;
        _entryQuestionEn = _entryQuestionAr = _entryAnswerEn = _entryAnswerAr = string.Empty;
        _entryOrder = "0";
        _entryActive = true;
        _entryModalOpen = true;
    }

    private void OpenEditEntry(AdminFaqEntrySummary e)
    {
        _error = null;
        _entryEditId = e.Id;
        _entryQuestionEn = e.Question;
        _entryQuestionAr = e.QuestionArabic;
        _entryAnswerEn = e.Answer;
        _entryAnswerAr = e.AnswerArabic;
        _entryOrder = e.DisplayOrder.ToString();
        _entryActive = e.IsActive;
        _entryModalOpen = true;
    }

    private void CloseEntryModal() => _entryModalOpen = false;

    private async Task SaveEntryAsync()
    {
        if (_selectedGroup is null || _busy) return;
        _error = null;
        if (string.IsNullOrWhiteSpace(_entryQuestionEn) || string.IsNullOrWhiteSpace(_entryQuestionAr)
            || string.IsNullOrWhiteSpace(_entryAnswerEn) || string.IsNullOrWhiteSpace(_entryAnswerAr))
        {
            _error = L["Admin.Faq.Entry.Required"];
            return;
        }
        _busy = true;
        _toast = null;
        try
        {
            int.TryParse(_entryOrder, out var order);
            ApiResult<AdminFaqEntrySummary>? env;
            if (_entryEditId is null)
            {
                env = await JS.InvokeAsync<ApiResult<AdminFaqEntrySummary>>(
                    "simfAccount.postJson", "/account/api/admin/faq/entries",
                    new CreateFaqEntryRequest
                    {
                        FaqGroupId = _selectedGroup.Id,
                        Question = _entryQuestionEn, QuestionArabic = _entryQuestionAr,
                        Answer = _entryAnswerEn, AnswerArabic = _entryAnswerAr,
                        DisplayOrder = order,
                    });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminFaqEntrySummary>>(
                    "simfAccount.putJson", $"/account/api/admin/faq/entries/{_entryEditId}",
                    new UpdateFaqEntryRequest
                    {
                        Question = _entryQuestionEn, QuestionArabic = _entryQuestionAr,
                        Answer = _entryAnswerEn, AnswerArabic = _entryAnswerAr,
                        DisplayOrder = order, IsActive = _entryActive,
                    });
            }
            if (env is { Success: true })
            {
                _entryModalOpen = false;
                _toast = new Toast("success", L["Admin.Faq.Saved"]);
                await LoadEntriesAsync();
                await LoadGroupsAsync(); // refresh entry counts
            }
            else
            {
                // Dialog still open — report in it, not behind it.
                _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    private async Task DeactivateEntryAsync(AdminFaqEntrySummary e)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/admin/faq/entries/{e.Id}");
        if (env is { Success: true })
        {
            _toast = new Toast("success", L["Admin.Faq.Deactivated"]);
            await LoadEntriesAsync();
            await LoadGroupsAsync();
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
        }
    }

    // §6.16 (F-U5-004) — the row Delete icon fired the destructive call on the
    // FIRST click, and these deletes CASCADE. Stage the action and make the admin
    // confirm; SimfConfirm is RequireExplicitClose so a stray backdrop click
    // cannot confirm it either.
    private (string Title, string Message, Func<Task> Run)? _pendingDelete;

    private void AskDelete(string title, string message, Func<Task> run) =>
        _pendingDelete = (title, message, run);

    private void CancelDelete() => _pendingDelete = null;

    private async Task ConfirmDeleteAsync()
    {
        var pending = _pendingDelete;
        _pendingDelete = null;
        if (pending is not null) await pending.Value.Run();
    }
}
