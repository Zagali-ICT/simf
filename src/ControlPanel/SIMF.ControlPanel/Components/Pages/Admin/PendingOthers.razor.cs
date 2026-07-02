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
using SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class PendingOthers
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminPendingUserSummary>? _page;
    private bool _loading;
    private bool _busy;
    private AdminPendingUserSummary? _rejectTarget;
    private string _rejectReason = string.Empty;
    // D-209 — bulk reject (shared-reason) modal state.
    private IReadOnlyList<AdminPendingUserSummary>? _bulkRejectSelected;
    private string _bulkRejectReason = string.Empty;
    private string? _toast;
    private string _toastVariant = "success";

    private AdminPendingUserSummary? _viewTarget;
    private PendingProfileResponse? _viewProfile;
    private bool _viewLoading;
    private string? _viewError;
    private bool _approveMode;

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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminPendingUserSummary>>>(
                "simfAccount.postJson", "/account/api/admin/others/pending/list", _query);
            _page = envelope is { Success: true, Data: not null }
                ? envelope.Data
                : GridPage<AdminPendingUserSummary>.Of(
                    Array.Empty<AdminPendingUserSummary>(), 0, _query);
        }
        finally { _loading = false; }
    }

    private async Task OnBulkApproveAsync(IReadOnlyList<AdminPendingUserSummary> selected)
    {
        if (_busy || selected.Count == 0) return;
        _busy = true;
        try
        {
            var ids = selected.Select(u => u.Id).ToList();
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkApprovalResponse>>(
                "simfAccount.postJson", "/account/api/admin/others/bulk-approve",
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

    private async Task ApproveAsync(AdminPendingUserSummary user)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/others/{user.Id}/approve", new { });
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

    private void OpenReject(AdminPendingUserSummary user)
    {
        _rejectTarget = user;
        _rejectReason = string.Empty;
        _toast = null;
    }

    private async Task ConfirmRejectAsync()
    {
        if (_busy || _rejectTarget is null) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/others/{_rejectTarget.Id}/reject",
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

    // D-209 — open the shared-reason modal with the selected pending rows.
    private void OnBulkRejectAsync(IReadOnlyList<AdminPendingUserSummary> selected)
    {
        if (_busy || selected.Count == 0) return;
        _bulkRejectSelected = selected;
        _bulkRejectReason = string.Empty;
        _toast = null;
    }

    private async Task ConfirmBulkRejectAsync()
    {
        if (_busy || _bulkRejectSelected is null) return;
        _busy = true;
        try
        {
            var ids = _bulkRejectSelected.Select(u => u.Id).ToList();
            var envelope = await JS.InvokeAsync<ApiResult<AdminBulkRejectResponse>>(
                "simfAccount.postJson", "/account/api/admin/others/bulk-reject",
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

    private string FormatSummary(int from, int to, int total) =>
        string.Format(L["Admin.Users.Pager.Summary"], from, to, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Pending.Pager.Page"], current, total);

    private async Task OpenViewAsync(AdminPendingUserSummary user, bool approveMode = false)
    {
        _viewTarget = user;
        _viewProfile = null;
        _viewError = null;
        _viewLoading = true;
        _approveMode = approveMode;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<PendingProfileResponse>>(
                "simfAccount.getJson",
                $"/account/api/admin/others/{user.Id}/profile-for-approval");
            if (envelope is { Success: true, Data: not null })
            {
                _viewProfile = envelope.Data;
            }
            else
            {
                _viewError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Pending.View.Fallback"];
            }
        }
        catch (Exception)
        {
            _viewError = L["Admin.Pending.View.Fallback"];
        }
        finally { _viewLoading = false; }
    }

    private async Task ConfirmApproveFromReviewAsync()
    {
        if (_viewTarget is null) return;
        var target = _viewTarget;
        CloseView();
        await ApproveAsync(target);
    }

    private void CloseView()
    {
        _viewTarget = null;
        _viewProfile = null;
        _viewError = null;
        _viewLoading = false;
        _approveMode = false;
    }

    private static bool HasProfileForm(PendingProfileResponse profile) =>
        !string.IsNullOrEmpty(profile.ArabicName)
        || !string.IsNullOrEmpty(profile.EnglishName)
        || !string.IsNullOrEmpty(profile.NationalityCode)
        || profile.DateOfBirth is not null
        || !string.IsNullOrEmpty(profile.PlaceOfBirth)
        || !string.IsNullOrEmpty(profile.NationalId)
        || !string.IsNullOrEmpty(profile.IqamaNumber)
        || !string.IsNullOrEmpty(profile.PassportNumber)
        || !string.IsNullOrEmpty(profile.SaudiMobile)
        || !string.IsNullOrEmpty(profile.InternationalMobile)
        || profile.HasIdImage
        || profile.InterestIds.Count > 0;

    private string IdentityTypeLabel(PendingProfileResponse profile)
    {
        if (!string.IsNullOrEmpty(profile.NationalId))
            return L["Admin.Pending.View.IdentityType.National"];
        if (!string.IsNullOrEmpty(profile.IqamaNumber))
            return L["Admin.Pending.View.IdentityType.Iqama"];
        if (!string.IsNullOrEmpty(profile.PassportNumber))
            return L["Admin.Pending.View.IdentityType.Passport"];
        return L["Admin.Pending.View.MissingValue"];
    }

    private static string? IdentityNumber(PendingProfileResponse profile) =>
        profile.NationalId
            ?? profile.IqamaNumber
            ?? profile.PassportNumber;
}
