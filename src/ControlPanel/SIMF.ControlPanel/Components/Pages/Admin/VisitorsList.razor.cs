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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VisitorsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "visitors";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminUserSummary>? _page;
    private bool _loading;
    private bool _busy;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private AdminUserSummary? _target;

    private bool _bulkDeleteOpen;
    private IReadOnlyList<AdminUserSummary> _bulkDeleteTargets = Array.Empty<AdminUserSummary>();
    private string _bulkDeleteReason = string.Empty;

    private bool _duplicateOpen;
    private AdminUserSummary? _duplicateSource;
    private string _duplicateEmail = string.Empty;

    // #10 — bulk badge generator, opened from the toolbar (gated Visitors.BulkGenerate).
    private bool _bulkAddOpen;

    private AdminImportUsersResponse? _importResult;
    private Toast? _toast;

    private bool FormOpen => _form != FormKind.None;
    // D-393 — the wide Add-visitor form opens full-page (it needs the room)
    // regardless of the admin's popup/page preference; Edit + Details keep the
    // preference.
    private CrudPresentation EffectivePresentation =>
        _form == FormKind.AddEdit && !_isEdit
            ? CrudPresentation.Page
            : _presentation;
    private bool GridHidden => FormOpen && EffectivePresentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Visitors.Edit.Title"]
            : L["Admin.Visitors.Add.Title"],
        FormKind.ViewDelete => string.Format(
            L["Admin.Visitors.Details.Title"], _target?.Email),
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

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminUserSummary>>>(
                "simfAccount.postJson", "/account/api/admin/visitors/list", _query);
            // §6.16 (F-U5-002) — a FAILED envelope used to be substituted with an
            // empty page, so an API 500 / 403 was indistinguishable from "no rows"
            // and the admin read a working page with no data. Report it instead;
            // the page already renders a toast surface it never used on this path.
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _page = GridPage<AdminUserSummary>.Of(Array.Empty<AdminUserSummary>(), 0, _query);
                ShowToast("error", envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Visitors.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private Task OnEditAsync(AdminUserSummary user)
    {
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = user;
        return Task.CompletedTask;
    }

    private Task OnDetailsAsync(AdminUserSummary user)
    {
        _form = FormKind.ViewDelete;
        _isEdit = false;
        _target = user;
        return Task.CompletedTask;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    private async Task OnSavedAsync(AdminUserSummary saved)
    {
        var wasEdit = _isEdit;
        CloseForm();
        ShowToast("success", wasEdit
            ? L["Admin.Edit.Saved"]
            : string.Format(L["Admin.CreateVisitor.Success"], saved.Email));
        await LoadAsync();
    }

    private Task OnRowDeleteAsync(AdminUserSummary user)
    {
        _bulkDeleteTargets = new[] { user };
        _bulkDeleteReason = string.Empty;
        _bulkDeleteOpen = true;
        return Task.CompletedTask;
    }

    private Task OnBulkDeleteAsync(IReadOnlyList<AdminUserSummary> users)
    {
        if (users.Count == 0)
        {
            ShowToast("error", L["Admin.Users.NoSelection"]);
            return Task.CompletedTask;
        }
        _bulkDeleteTargets = users;
        _bulkDeleteReason = string.Empty;
        _bulkDeleteOpen = true;
        return Task.CompletedTask;
    }

    private async Task ConfirmBulkDeleteAsync()
    {
        if (_bulkDeleteReason.Length is < 10 or > 500 || _bulkDeleteTargets.Count == 0) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkDeleteResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/bulk-delete",
                new AdminBulkDeleteRequest
                {
                    Ids = _bulkDeleteTargets.Select(u => u.Id).ToList(),
                    Reason = _bulkDeleteReason,
                });
            if (envelope is { Success: true, Data: not null })
            {
                _bulkDeleteOpen = false;
                ShowToast("success", string.Format(
                    L["Admin.Users.BulkDelete.Success"],
                    envelope.Data.Deleted, envelope.Data.Skipped));
                await LoadAsync();
            }
            else
            {
                ShowToast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.BulkDelete.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private Task OnDuplicateAsync(AdminUserSummary user)
    {
        _duplicateSource = user;
        _duplicateEmail = string.Empty;
        _duplicateOpen = true;
        return Task.CompletedTask;
    }

    private async Task ConfirmDuplicateAsync()
    {
        if (_duplicateSource is null || !IsLikelyEmail(_duplicateEmail)) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminCreateUserResponse>>(
                "simfAccount.postJson", "/account/api/admin/visitors/duplicate",
                new AdminDuplicateUserRequest
                {
                    SourceId = _duplicateSource.Id,
                    NewEmail = _duplicateEmail,
                });
            if (envelope is { Success: true, Data: not null })
            {
                _duplicateOpen = false;
                ShowToast("success", string.Format(
                    L["Admin.Users.Duplicate.Success"], envelope.Data.Email));
                await LoadAsync();
            }
            else
            {
                ShowToast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.Duplicate.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private Task OnCopySelectedAsync(IReadOnlyList<AdminUserSummary> users)
    {
        if (users.Count == 0) return Task.CompletedTask;
        ShowToast("info", string.Format(L["Admin.Users.Copy.Count"], users.Count));
        return Task.CompletedTask;
    }

    private Task OnCopyOneAsync(AdminUserSummary user)
    {
        ShowToast("info", string.Format(L["Admin.Users.Copy.One"], user.Email));
        return Task.CompletedTask;
    }

    private Task OnPasteAsync(string clipboard)
    {
        if (string.IsNullOrWhiteSpace(clipboard))
        {
            ShowToast("error", L["Admin.Users.Paste.Empty"]);
            return Task.CompletedTask;
        }
        ShowToast("info", L["Admin.Users.Paste.NotImplemented"]);
        return Task.CompletedTask;
    }

    private async Task OnExportAsync(IReadOnlyList<AdminUserSummary> selected)
    {
        var ids = selected.Select(u => u.Id).ToList();
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/visitors/export",
            new AdminExportUsersRequest { Ids = ids, Query = ids.Count == 0 ? _query : null }, L);
        if (error is not null) ShowToast("error", error);
    }

    private async Task OnImportAsync() =>
        await JS.InvokeVoidAsync("simfAccount.triggerFileInput", "visitors-import-input");

    private async Task OnImportFileSelectedAsync()
    {
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminImportUsersResponse>>(
                "simfAccount.uploadFile", "/account/api/admin/visitors/import", "visitors-import-input");
            if (envelope is { Success: true, Data: not null })
            {
                _importResult = envelope.Data;
                await LoadAsync();
            }
            else
            {
                ShowToast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.Import.Fallback"]);
            }
        }
        finally { _busy = false; }
    }

    private string FormatSummary(int from, int to, int total) =>
        string.Format(L["Admin.Users.Pager.Summary"], from, to, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Users.Pager.Page"], current, total);

    // The row's avatar thumbnail URL, or null when the account has no photo so
    // SimfIdentityCell shows an initials tile (never a broken image). The CP BFF
    // streams the bytes from the StoredFile avatar (D-568); only requested when
    // HasAvatar is set so the grid never issues a 404.
    private static string? AvatarImageUrl(AdminUserSummary row) =>
        row.HasAvatar ? $"/account/api/admin/visitors/{row.Id}/avatar" : null;

    private static bool IsLikelyEmail(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains('@', StringComparison.Ordinal)
        && value.Length <= 256;

    private void ShowToast(string variant, string message)
    {
        _toast = new Toast(variant, message);
    }

    private sealed record Toast(string Variant, string Message);
}
