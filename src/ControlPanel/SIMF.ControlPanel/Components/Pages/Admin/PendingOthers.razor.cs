using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;
using Microsoft.JSInterop;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>P7e (D-055) — the pending Other-typed approval queue. The shared grid /
/// approve / reject / bulk logic lives in <see cref="PendingApprovalPageBase"/>
/// (D-641); this partial pins the <c>others</c> API segment and adds the
/// profile-review modal (D-124 preview, D-128 review-then-approve).</summary>
public partial class PendingOthers
{
    protected override string ApiBase => "others";

    // D-353 parity — the review "View" modal honours the popup/full-page toggle,
    // persisted per-user under this key. ViewFullPage hides the grid while the
    // review takes the full page.
    protected override string? PresentationPageKey => "pending-others";

    private bool ViewFullPage =>
        _viewTarget is not null && _presentation == CrudPresentation.Page;

    // The pending row's avatar thumbnail URL, or null so SimfIdentityCell shows an
    // initials tile (never a broken image). Only when HasAvatar is set (D-568).
    private static string? AvatarImageUrl(AdminPendingUserSummary row) =>
        row.HasAvatar ? $"/account/api/admin/others/{row.Id}/avatar" : null;

    private AdminPendingUserSummary? _viewTarget;
    private PendingProfileResponse? _viewProfile;
    private bool _viewLoading;
    private string? _viewError;
    private bool _approveMode;

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
