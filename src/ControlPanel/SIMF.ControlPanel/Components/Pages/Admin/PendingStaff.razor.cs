using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>The pending-staff (admin-typed) approval queue. All grid /
/// approve / reject / bulk logic lives in <see cref="PendingApprovalPageBase"/>;
/// this partial pins the <c>admins</c> API user-type segment and owns the
/// single-approve confirmation, which is this queue's alone: the
/// visitors/others queues stage a single approval through their
/// profile-review modal, and an administrator candidate has no profile to show.</summary>
public partial class PendingStaff
{
    protected override string ApiBase => "admins";

    private AdminPendingUserSummary? _approveTarget;

    private void OpenApprove(AdminPendingUserSummary user)
    {
        _approveTarget = user;
        _toast = null;
    }

    private void CancelApprove() => _approveTarget = null;

    /// <summary>Closes the confirmation before approving so the resulting toast
    /// is not hidden behind the dialog (the review-modal ordering).</summary>
    private async Task ConfirmApproveAsync()
    {
        if (_busy || _approveTarget is null) return;
        var user = _approveTarget;
        _approveTarget = null;
        await ApproveAsync(user);
    }
}
