using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Feedback;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RatingConfig
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _typeQuery = new() { Top = 20 };
    private GridPage<AdminRatingTypeSummary> _typePage = new();
    private bool _typeLoading;

    private AdminRatingTypeSummary? _selectedType;
    private GridQuery _groupQuery = new() { Top = 50 };
    private GridPage<AdminRatingQuestionGroupSummary> _groupPage = new();
    private bool _groupLoading;
    private GridQuery _questionQuery = new() { Top = 50 };
    private GridPage<AdminRatingQuestionSummary> _questionPage = new();
    private bool _questionLoading;

    private bool _busy;
    private Toast? _toast;

    // Type modal state.
    private bool _typeModalOpen;
    private Guid? _typeEditId;
    private bool _typeIsSystem;
    private string _typeCode = string.Empty;
    private string _typeNameEn = string.Empty;
    private string _typeNameAr = string.Empty;
    private RatingScope _typeScope = RatingScope.Global;
    private bool _typeHasOverall = true;
    private bool _typeAllowComment = true;
    private string _typeCommentLabelEn = string.Empty;
    private string _typeCommentLabelAr = string.Empty;
    private string _typeOrder = "0";
    private bool _typeActive = true;

    // Group modal state.
    private bool _groupModalOpen;
    private Guid? _groupEditId;
    private string _groupNameEn = string.Empty;
    private string _groupNameAr = string.Empty;
    private string _groupOrder = "0";
    private bool _groupActive = true;

    // Question modal state.
    private bool _questionModalOpen;
    private Guid? _questionEditId;
    private string _questionTextEn = string.Empty;
    private string _questionTextAr = string.Empty;
    private string _questionGroupId = string.Empty;
    private bool _questionRequired;
    private string _questionOrder = "0";
    private bool _questionActive = true;

    protected override async Task OnInitializedAsync() => await LoadTypesAsync();

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private string ScopeLabel(RatingScope scope) => scope == RatingScope.PerSession
        ? L["Admin.RatingConfig.Scope.PerSession"]
        : L["Admin.RatingConfig.Scope.Global"];

    private string GroupName(Guid? id) =>
        id is { } gid && _groupPage.Items.FirstOrDefault(g => g.Id == gid) is { } g ? g.Name : "—";

    // -- Types --
    private async Task OnTypeQueryChangedAsync(GridQuery next)
    {
        _typeQuery = next;
        await LoadTypesAsync();
    }

    private async Task LoadTypesAsync()
    {
        _typeLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminRatingTypeSummary>>>(
                "simfAccount.postJson", "/account/api/admin/ratings/types/list", _typeQuery);
            if (env is { Success: true, Data: not null }) { _typePage = env.Data; }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _typeLoading = false; }
    }

    private async Task SelectTypeAsync(AdminRatingTypeSummary type)
    {
        _selectedType = type;
        _groupQuery = new GridQuery { Top = 50 };
        _questionQuery = new GridQuery { Top = 50 };
        await LoadGroupsAsync();
        await LoadQuestionsAsync();
    }

    private void OpenAddType()
    {
        _typeEditId = null;
        _typeIsSystem = false;
        _typeCode = _typeNameEn = _typeNameAr = _typeCommentLabelEn = _typeCommentLabelAr = string.Empty;
        _typeScope = RatingScope.Global;
        _typeHasOverall = _typeAllowComment = true;
        _typeOrder = "0";
        _typeActive = true;
        _typeModalOpen = true;
    }

    private void OpenEditType(AdminRatingTypeSummary t)
    {
        _typeEditId = t.Id;
        _typeIsSystem = t.IsSystem;
        _typeCode = t.Code;
        _typeNameEn = t.Name;
        _typeNameAr = t.NameArabic;
        _typeScope = t.Scope;
        _typeHasOverall = t.HasOverallStars;
        _typeAllowComment = t.AllowComment;
        _typeCommentLabelEn = t.CommentLabel ?? string.Empty;
        _typeCommentLabelAr = t.CommentLabelArabic ?? string.Empty;
        _typeOrder = t.DisplayOrder.ToString();
        _typeActive = t.IsActive;
        _typeModalOpen = true;
    }

    private void CloseTypeModal() => _typeModalOpen = false;

    private async Task SaveTypeAsync()
    {
        _busy = true;
        _toast = null;
        try
        {
            int.TryParse(_typeOrder, out var order);
            ApiResult<AdminRatingTypeSummary>? env;
            if (_typeEditId is null)
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingTypeSummary>>(
                    "simfAccount.postJson", "/account/api/admin/ratings/types",
                    new CreateRatingTypeRequest
                    {
                        Code = _typeCode, Name = _typeNameEn, NameArabic = _typeNameAr,
                        Scope = _typeScope, HasOverallStars = _typeHasOverall, AllowComment = _typeAllowComment,
                        CommentLabel = _typeCommentLabelEn, CommentLabelArabic = _typeCommentLabelAr,
                        DisplayOrder = order,
                    });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingTypeSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/ratings/types/{_typeEditId}",
                    new UpdateRatingTypeRequest
                    {
                        Name = _typeNameEn, NameArabic = _typeNameAr,
                        HasOverallStars = _typeHasOverall, AllowComment = _typeAllowComment,
                        CommentLabel = _typeCommentLabelEn, CommentLabelArabic = _typeCommentLabelAr,
                        DisplayOrder = order, IsActive = _typeActive,
                    });
            }
            if (env is { Success: true }) { _typeModalOpen = false; _toast = new Toast("success", L["Admin.RatingConfig.Saved"]); await LoadTypesAsync(); }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _busy = false; }
    }

    private async Task DeactivateTypeAsync(AdminRatingTypeSummary t)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/admin/ratings/types/{t.Id}");
        if (env is { Success: true })
        {
            _toast = new Toast("success", L["Admin.RatingConfig.Deactivated"]);
            if (_selectedType?.Id == t.Id) { _selectedType = null; }
            await LoadTypesAsync();
        }
        else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
    }

    // -- Groups --
    private async Task OnGroupQueryChangedAsync(GridQuery next) { _groupQuery = next; await LoadGroupsAsync(); }

    private async Task LoadGroupsAsync()
    {
        if (_selectedType is null) return;
        _groupLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminRatingQuestionGroupSummary>>>(
                "simfAccount.postJson", $"/account/api/admin/ratings/types/{_selectedType.Id}/groups/list", _groupQuery);
            if (env is { Success: true, Data: not null }) { _groupPage = env.Data; }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _groupLoading = false; }
    }

    private void OpenAddGroup()
    {
        _groupEditId = null;
        _groupNameEn = _groupNameAr = string.Empty;
        _groupOrder = "0";
        _groupActive = true;
        _groupModalOpen = true;
    }

    private void OpenEditGroup(AdminRatingQuestionGroupSummary g)
    {
        _groupEditId = g.Id;
        _groupNameEn = g.Name;
        _groupNameAr = g.NameArabic;
        _groupOrder = g.DisplayOrder.ToString();
        _groupActive = g.IsActive;
        _groupModalOpen = true;
    }

    private void CloseGroupModal() => _groupModalOpen = false;

    private async Task SaveGroupAsync()
    {
        if (_selectedType is null) return;
        _busy = true;
        _toast = null;
        try
        {
            int.TryParse(_groupOrder, out var order);
            ApiResult<AdminRatingQuestionGroupSummary>? env;
            if (_groupEditId is null)
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingQuestionGroupSummary>>(
                    "simfAccount.postJson", "/account/api/admin/ratings/groups",
                    new CreateRatingQuestionGroupRequest { RatingTypeId = _selectedType.Id, Name = _groupNameEn, NameArabic = _groupNameAr, DisplayOrder = order });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingQuestionGroupSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/ratings/groups/{_groupEditId}",
                    new UpdateRatingQuestionGroupRequest { Name = _groupNameEn, NameArabic = _groupNameAr, DisplayOrder = order, IsActive = _groupActive });
            }
            if (env is { Success: true }) { _groupModalOpen = false; _toast = new Toast("success", L["Admin.RatingConfig.Saved"]); await LoadGroupsAsync(); await LoadTypesAsync(); }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _busy = false; }
    }

    private async Task DeactivateGroupAsync(AdminRatingQuestionGroupSummary g)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/admin/ratings/groups/{g.Id}");
        if (env is { Success: true }) { _toast = new Toast("success", L["Admin.RatingConfig.Deactivated"]); await LoadGroupsAsync(); await LoadQuestionsAsync(); await LoadTypesAsync(); }
        else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
    }

    // -- Questions --
    private async Task OnQuestionQueryChangedAsync(GridQuery next) { _questionQuery = next; await LoadQuestionsAsync(); }

    private async Task LoadQuestionsAsync()
    {
        if (_selectedType is null) return;
        _questionLoading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminRatingQuestionSummary>>>(
                "simfAccount.postJson", $"/account/api/admin/ratings/types/{_selectedType.Id}/questions/list", _questionQuery);
            if (env is { Success: true, Data: not null }) { _questionPage = env.Data; }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _questionLoading = false; }
    }

    private void OpenAddQuestion()
    {
        _questionEditId = null;
        _questionTextEn = _questionTextAr = string.Empty;
        _questionGroupId = string.Empty;
        _questionRequired = false;
        _questionOrder = "0";
        _questionActive = true;
        _questionModalOpen = true;
    }

    private void OpenEditQuestion(AdminRatingQuestionSummary q)
    {
        _questionEditId = q.Id;
        _questionTextEn = q.Text;
        _questionTextAr = q.TextArabic;
        _questionGroupId = q.RatingQuestionGroupId?.ToString() ?? string.Empty;
        _questionRequired = q.IsRequired;
        _questionOrder = q.DisplayOrder.ToString();
        _questionActive = q.IsActive;
        _questionModalOpen = true;
    }

    private void CloseQuestionModal() => _questionModalOpen = false;

    private async Task SaveQuestionAsync()
    {
        if (_selectedType is null) return;
        _busy = true;
        _toast = null;
        try
        {
            int.TryParse(_questionOrder, out var order);
            Guid? groupId = Guid.TryParse(_questionGroupId, out var gid) ? gid : null;
            ApiResult<AdminRatingQuestionSummary>? env;
            if (_questionEditId is null)
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingQuestionSummary>>(
                    "simfAccount.postJson", "/account/api/admin/ratings/questions",
                    new CreateRatingQuestionRequest
                    {
                        RatingTypeId = _selectedType.Id, RatingQuestionGroupId = groupId,
                        Text = _questionTextEn, TextArabic = _questionTextAr,
                        IsRequired = _questionRequired, DisplayOrder = order,
                    });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminRatingQuestionSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/ratings/questions/{_questionEditId}",
                    new UpdateRatingQuestionRequest
                    {
                        RatingQuestionGroupId = groupId,
                        Text = _questionTextEn, TextArabic = _questionTextAr,
                        IsRequired = _questionRequired, DisplayOrder = order, IsActive = _questionActive,
                    });
            }
            if (env is { Success: true }) { _questionModalOpen = false; _toast = new Toast("success", L["Admin.RatingConfig.Saved"]); await LoadQuestionsAsync(); await LoadGroupsAsync(); await LoadTypesAsync(); }
            else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
        }
        finally { _busy = false; }
    }

    private async Task DeactivateQuestionAsync(AdminRatingQuestionSummary q)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<bool>>(
            "simfAccount.deleteJson", $"/account/api/admin/ratings/questions/{q.Id}");
        if (env is { Success: true }) { _toast = new Toast("success", L["Admin.RatingConfig.Deactivated"]); await LoadQuestionsAsync(); await LoadTypesAsync(); }
        else { _toast = new Toast("error", env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]); }
    }
}
