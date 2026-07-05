using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
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

    // Group modal state.
    private bool _groupModalOpen;
    private Guid? _groupEditId;
    private string _groupNameEn = string.Empty;
    private string _groupNameAr = string.Empty;
    private string _groupOrder = "0";
    private bool _groupActive = true;

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
        _groupNameEn = _groupNameAr = string.Empty;
        _groupOrder = "0";
        _groupActive = true;
        _groupModalOpen = true;
    }

    private void OpenEditGroup(AdminFaqGroupSummary g)
    {
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
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
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
        _entryQuestionEn = _entryQuestionAr = _entryAnswerEn = _entryAnswerAr = string.Empty;
        _entryOrder = "0";
        _entryActive = true;
        _entryModalOpen = true;
    }

    private void OpenEditEntry(AdminFaqEntrySummary e)
    {
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
        if (_selectedGroup is null) return;
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
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture() ?? L["Admin.Faq.LoadFailed"]);
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
}
