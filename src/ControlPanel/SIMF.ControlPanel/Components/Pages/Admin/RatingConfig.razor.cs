using Microsoft.AspNetCore.Components;
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

    /// <summary>The in-dialog error. Separate from <c>_toast</c> because the
    /// page-level alert renders under the modal backdrop and is invisible while
    /// a modal is open.</summary>
    private string? _error;

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

    /// <summary>D-835 - the records being read. Both grids already hold the whole
    /// summary, so Details opens straight from the row: no second fetch, and no
    /// permission of its own, because reading the row is what RatingConfig.View
    /// already bought.</summary>
    private AdminRatingTypeSummary? _typeDetails;
    private AdminRatingQuestionGroupSummary? _groupDetails;
    private AdminRatingQuestionSummary? _questionDetails;

    private void OpenTypeDetails(AdminRatingTypeSummary type) => _typeDetails = type;

    private void OpenGroupDetails(AdminRatingQuestionGroupSummary group) => _groupDetails = group;

    private void OpenQuestionDetails(AdminRatingQuestionSummary question) => _questionDetails = question;

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

    private string ScopeLabel(RatingScope scope) => scope switch
    {
        RatingScope.PerSession => L["Admin.RatingConfig.Scope.PerSession"],
        RatingScope.PerDay => L["Admin.RatingConfig.Scope.PerDay"],
        _ => L["Admin.RatingConfig.Scope.Global"],
    };

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
        _error = null;
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
        _error = null;
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
        if (_busy) return;
        _error = null;
        // Code is create-only (locked on edit), so it is required only there.
        if ((_typeEditId is null && string.IsNullOrWhiteSpace(_typeCode))
            || string.IsNullOrWhiteSpace(_typeNameEn) || string.IsNullOrWhiteSpace(_typeNameAr))
        {
            _error = L["Admin.RatingConfig.Type.Required"];
            return;
        }
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
            // Dialog still open — report in it, not behind it.
            else { _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]; }
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
        _error = null;
        _groupNameEn = _groupNameAr = string.Empty;
        _groupOrder = "0";
        _groupActive = true;
        _groupModalOpen = true;
    }

    private void OpenEditGroup(AdminRatingQuestionGroupSummary g)
    {
        _error = null;
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
        if (_selectedType is null || _busy) return;
        _error = null;
        if (string.IsNullOrWhiteSpace(_groupNameEn) || string.IsNullOrWhiteSpace(_groupNameAr))
        {
            _error = L["Admin.RatingConfig.Group.Required"];
            return;
        }
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
            // Dialog still open — report in it, not behind it.
            else { _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]; }
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
        _error = null;
        _questionTextEn = _questionTextAr = string.Empty;
        _questionGroupId = string.Empty;
        _questionRequired = false;
        _questionOrder = "0";
        _questionActive = true;
        _questionModalOpen = true;
    }

    private void OpenEditQuestion(AdminRatingQuestionSummary q)
    {
        _error = null;
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
        if (_selectedType is null || _busy) return;
        _error = null;
        if (string.IsNullOrWhiteSpace(_questionTextEn) || string.IsNullOrWhiteSpace(_questionTextAr))
        {
            _error = L["Admin.RatingConfig.Question.Required"];
            return;
        }
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
            // Dialog still open — report in it, not behind it.
            else { _error = env?.Error?.MessageForCurrentCulture() ?? L["Admin.RatingConfig.LoadFailed"]; }
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
