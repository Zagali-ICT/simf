// Tests: SIMF.ControlPanel.Tests/PendingApprovalQueueTests.cs
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>
/// Shared logic for the three pending-approval queues (admins / others /
/// visitors): the grid load, single + bulk approve/reject, the reject-modal
/// state, and the toast + pager formatters. Each queue's <c>.razor</c> keeps its
/// own divergent RowActions and (Others/Visitors) profile-review modal — only the
/// admin API user-type segment differs, exposed as <see cref="ApiBase"/>. Mirrors
/// the established <c>CrudAddEditFormBase</c> base-class pattern (D-641).
/// </summary>
public abstract class PendingApprovalPageBase : ComponentBase
{
    [Inject] protected IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected CpPreferences Prefs { get; set; } = default!;

    /// <summary>The admin API user-type segment for this queue:
    /// <c>admins</c> / <c>others</c> / <c>visitors</c>.</summary>
    protected abstract string ApiBase { get; }

    // Popup/full-page presentation for the profile-review "View" modal, extending
    // the D-353 CRUD framing to the pending queues so it matches /admin/visitors.
    // Only the visitors + others queues have a review modal (admins approve
    // one-click); a queue opts in via PresentationPageKey (null = dialog only).
    protected CrudPresentation _presentation = CrudPresentation.Dialog;

    /// <summary>The localStorage key the popup/full-page choice persists under
    /// (D-353), or null for queues without a review modal (the admins queue).</summary>
    protected virtual string? PresentationPageKey => null;

    protected GridQuery _query = new() { Top = 20 };
    protected GridPage<AdminPendingUserSummary>? _page;
    protected bool _loading;
    protected bool _busy;
    protected AdminPendingUserSummary? _rejectTarget;
    protected string _rejectReason = string.Empty;
    protected IReadOnlyList<AdminPendingUserSummary>? _bulkRejectSelected;
    protected string _bulkRejectReason = string.Empty;
    protected string? _toast;
    protected string _toastVariant = "success";

    protected override async Task OnInitializedAsync()
    {
        if (PresentationPageKey is not null)
        {
            _presentation = await Prefs.GetPresentationAsync(PresentationPageKey);
        }
        await LoadAsync();
    }

    protected async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    protected async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminPendingUserSummary>>>(
                "simfAccount.postJson", $"/account/api/admin/{ApiBase}/pending/list", _query);
            _page = envelope is { Success: true, Data: not null }
                ? envelope.Data
                : GridPage<AdminPendingUserSummary>.Of(
                    Array.Empty<AdminPendingUserSummary>(), 0, _query);
        }
        finally { _loading = false; }
    }

    /// <summary>Approves the user. <paramref name="approveBody"/> lets a queue send
    /// a richer payload (the visitors queue posts a chosen ProfileTypeId); the
    /// admins/others queues pass none, keeping the original empty-object body.</summary>
    protected async Task ApproveAsync(AdminPendingUserSummary user, object? approveBody = null)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/{ApiBase}/{user.Id}/approve", approveBody ?? new { });
            if (envelope is { Success: true })
            {
                _toast = string.Format(L["Admin.Pending.Approved.Toast"], user.Email);
                _toastVariant = "success";
                await LoadAsync();
            }
            else
            {
                _toast = envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Users.Fallback"];
                _toastVariant = "error";
            }
        }
        finally { _busy = false; }
    }

    protected void OpenReject(AdminPendingUserSummary user)
    {
        _rejectTarget = user;
        _rejectReason = string.Empty;
        _toast = null;
    }

    protected async Task ConfirmRejectAsync()
    {
        if (_busy || _rejectTarget is null) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/{ApiBase}/{_rejectTarget.Id}/reject",
                new AdminRejectRequest { Reason = _rejectReason });
            if (envelope is { Success: true })
            {
                _toast = string.Format(L["Admin.Pending.Rejected.Toast"], _rejectTarget.Email);
                _toastVariant = "success";
                _rejectTarget = null;
                _rejectReason = string.Empty;
                await LoadAsync();
            }
            else
            {
                _toast = envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.Users.Fallback"];
                _toastVariant = "error";
            }
        }
        finally { _busy = false; }
    }

    protected async Task OnBulkApproveAsync(IReadOnlyList<AdminPendingUserSummary> selected)
    {
        if (_busy || selected.Count == 0) return;
        _busy = true;
        try
        {
            var ids = selected.Select(u => u.Id).ToList();
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkApprovalResponse>>(
                "simfAccount.postJson", $"/account/api/admin/{ApiBase}/bulk-approve",
                new AdminBulkApprovalRequest { Ids = ids });
            if (envelope is { Success: true, Data: not null })
            {
                var result = envelope.Data;
                _toast = string.Format(
                    L["Admin.Pending.BulkApproved.Toast"],
                    result.Approved, result.Skipped);
                _toastVariant = result.Skipped == 0 ? "success" : "warning";
                await LoadAsync();
            }
            else
            {
                _toast = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.Fallback"];
                _toastVariant = "error";
            }
        }
        finally { _busy = false; }
    }

    protected void OnBulkRejectAsync(IReadOnlyList<AdminPendingUserSummary> selected)
    {
        if (_busy || selected.Count == 0) return;
        _bulkRejectSelected = selected;
        _bulkRejectReason = string.Empty;
        _toast = null;
    }

    protected async Task ConfirmBulkRejectAsync()
    {
        if (_busy || _bulkRejectSelected is null) return;
        _busy = true;
        try
        {
            var ids = _bulkRejectSelected.Select(u => u.Id).ToList();
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkRejectResponse>>(
                "simfAccount.postJson", $"/account/api/admin/{ApiBase}/bulk-reject",
                new AdminBulkRejectRequest { Ids = ids, Reason = _bulkRejectReason });
            if (envelope is { Success: true, Data: not null })
            {
                var result = envelope.Data;
                _toast = string.Format(
                    L["Admin.Pending.BulkRejected.Toast"],
                    result.Rejected, result.Skipped);
                _toastVariant = result.Skipped == 0 ? "success" : "warning";
                _bulkRejectSelected = null;
                _bulkRejectReason = string.Empty;
                await LoadAsync();
            }
            else
            {
                _toast = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Users.Fallback"];
                _toastVariant = "error";
            }
        }
        finally { _busy = false; }
    }

    protected string FormatSummary(int from, int to, int total) =>
        string.Format(L["Admin.Users.Pager.Summary"], from, to, total);

    protected string FormatPage(int current, int total) =>
        string.Format(L["Admin.Pending.Pager.Page"], current, total);
}
