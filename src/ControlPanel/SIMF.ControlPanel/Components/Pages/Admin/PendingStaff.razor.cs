namespace SIMF.ControlPanel.Components.Pages.Admin;

/// <summary>P4b — the pending-staff (admin-typed) approval queue. All grid /
/// approve / reject / bulk logic lives in <see cref="PendingApprovalPageBase"/>;
/// staff approve directly (no profile-review modal), so this partial only pins
/// the <c>admins</c> API user-type segment (D-641).</summary>
public partial class PendingStaff
{
    protected override string ApiBase => "admins";
}
