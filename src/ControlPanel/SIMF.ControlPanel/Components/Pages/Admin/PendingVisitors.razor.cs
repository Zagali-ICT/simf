using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class PendingVisitors
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

    // D-125 — View modal state for the D-124 pending-profile preview.
    // D-128 — _approveMode reuses the same modal for the
    // review-before-approve confirmation flow.
    private AdminPendingUserSummary? _viewTarget;
    private PendingProfileResponse? _viewProfile;
    private bool _viewLoading;
    private string? _viewError;
    private bool _approveMode;

    // CS-E (D-387) — full-size image lightbox. _imageUrl is captured once when
    // the view modal opens so the cache-buster stays stable across re-renders.
    private bool _imageZoomOpen;
    private string? _imageUrl;
    // CS-4 — profile photo (avatar) lightbox state, parallel to the ID image.
    private bool _avatarZoomOpen;
    private string? _avatarUrl;

    // CS-D (D-386) — approve-modal tier picker state. The list holds the
    // active audience-side profile types; the selection (null = keep current)
    // is sent in the approve POST.
    private AdminProfileTypeSummary[] _approveProfileTypes = Array.Empty<AdminProfileTypeSummary>();
    private Guid? _approveProfileTypeId;

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
                "simfAccount.postJson", "/account/api/admin/visitors/pending/list", _query);
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
                "simfAccount.postJson", "/account/api/admin/visitors/bulk-approve",
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

    private async Task ApproveAsync(AdminPendingUserSummary user, Guid? profileTypeId = null)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/visitors/{user.Id}/approve",
                new { ProfileTypeId = profileTypeId });
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
                $"/account/api/admin/visitors/{_rejectTarget.Id}/reject",
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
                "simfAccount.postJson", "/account/api/admin/visitors/bulk-reject",
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
        _imageZoomOpen = false;
        _imageUrl = null;
        _avatarZoomOpen = false;
        _avatarUrl = null;
        _approveMode = approveMode;
        _approveProfileTypeId = null;
        _approveProfileTypes = Array.Empty<AdminProfileTypeSummary>();
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<PendingProfileResponse>>(
                "simfAccount.getJson",
                $"/account/api/admin/visitors/{user.Id}/profile-for-approval");
            if (envelope is { Success: true, Data: not null })
            {
                _viewProfile = envelope.Data;
                // CS-E (D-387) — capture the cache-buster URL once so the
                // thumbnail and lightbox share a stable src and don't re-fetch.
                _imageUrl = _viewProfile.HasIdImage
                    ? $"/account/api/admin/visitors/{_viewProfile.Id}/id-document?v={DateTime.UtcNow.Ticks}"
                    : null;
                // CS-4 — same capture for the profile photo (avatar).
                _avatarUrl = _viewProfile.HasAvatar
                    ? $"/account/api/admin/visitors/{_viewProfile.Id}/avatar?v={DateTime.UtcNow.Ticks}"
                    : null;
                // CS-D (D-386) — only the approve flow needs the tier picker.
                // Reuse the same list call the walk-in form uses, then keep
                // only the active audience-side rows.
                // D-392 (owner: "default is 0 normal") — pre-select the seeded
                // "Normal" audience type by default rather than the visitor's
                // current tier, so the desk just reviews the photo/data and
                // approves. Falls back to the visitor's current tier if a
                // "Normal" row is somehow absent.
                if (_approveMode)
                {
                    await LoadApproveProfileTypesAsync();
                    var normal = _approveProfileTypes.FirstOrDefault(p =>
                        string.Equals(p.Name, "Normal", StringComparison.OrdinalIgnoreCase));
                    _approveProfileTypeId = normal?.Id ?? _viewProfile.ProfileTypeId;
                }
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

    private async Task LoadApproveProfileTypesAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        _approveProfileTypes = envelope is { Success: true, Data: not null }
            ? envelope.Data.Where(p => p.IsActive && p.IsVisitor)
                .OrderBy(p => p.Name).ToArray()
            : Array.Empty<AdminProfileTypeSummary>();
    }

    private void OnApproveProfileTypeChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _approveProfileTypeId = Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string ProfileTypeLabel(AdminProfileTypeSummary pt) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? pt.NameArabic
            : pt.Name;

    private async Task ConfirmApproveFromReviewAsync()
    {
        if (_viewTarget is null) return;
        var target = _viewTarget;
        // CS-D (D-386) — carry the optionally-picked tier (null = keep current).
        var profileTypeId = _approveProfileTypeId;
        // Close the review modal before firing the approve call so the
        // toast that surfaces from ApproveAsync isn't covered by it.
        CloseView();
        await ApproveAsync(target, profileTypeId);
    }

    private void CloseView()
    {
        _viewTarget = null;
        _viewProfile = null;
        _viewError = null;
        _viewLoading = false;
        _imageZoomOpen = false;
        _imageUrl = null;
        _avatarZoomOpen = false;
        _avatarUrl = null;
        _approveMode = false;
        _approveProfileTypeId = null;
        _approveProfileTypes = Array.Empty<AdminProfileTypeSummary>();
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

    private string GenderLabel(PendingProfileResponse p) => p.Gender switch
    {
        "Male" => L["Admin.Pending.View.Gender.Male"].Value,
        "Female" => L["Admin.Pending.View.Gender.Female"].Value,
        _ => L["Admin.Pending.View.MissingValue"].Value,
    };

    private string OrganisationLabel(PendingProfileResponse p)
    {
        var ar = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var name = ar
            ? (p.OrganisationNameArabic ?? p.OrganisationName)
            : (p.OrganisationName ?? p.OrganisationNameArabic);
        return string.IsNullOrEmpty(name) ? L["Admin.Pending.View.MissingValue"].Value : name;
    }

    private string InterestsLabel(PendingProfileResponse p)
    {
        if (p.Interests is null || p.Interests.Count == 0)
        {
            return L["Admin.Pending.View.MissingValue"].Value;
        }
        var ar = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var names = p.Interests.Select(i => ar
            ? (string.IsNullOrEmpty(i.NameArabic) ? i.Name : i.NameArabic)
            : (string.IsNullOrEmpty(i.Name) ? i.NameArabic : i.Name));
        return string.Join(ar ? "، " : ", ", names);
    }
}
