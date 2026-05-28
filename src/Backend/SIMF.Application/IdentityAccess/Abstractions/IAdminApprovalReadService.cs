using SIMF.Contracts.Admin;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// D-124 — scoped read of a pending-approval user's full profile, used
/// by the Control Panel approve / reject flow to preview the application
/// before the admin commits. The reads are intentionally narrow:
///
/// <list type="bullet">
///   <item>The target must currently be in <c>AccountState.PendingApproval</c>.</item>
///   <item>The target's <c>UserType</c> must match the method called.</item>
/// </list>
///
/// <para>A target that fails either guard returns <c>null</c>, which the
/// endpoint then translates to a 404. The 404-for-all-mismatch stance
/// is load-bearing — it stops an admin enumerating approved users or
/// cross-type ids via error-code diff (same guard <see cref="D-113"/>
/// took for type-smuggling).</para>
/// </summary>
public interface IAdminApprovalReadService
{
    /// <summary>Pending-visitor profile preview. Returns null when the id
    /// doesn't exist, the target isn't Pending, or the UserType is not
    /// Visitor.</summary>
    Task<PendingProfileResponse?> GetPendingVisitorProfileAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Pending-Other profile preview — same shape, restricted to
    /// <see cref="SIMF.Common.Enums.UserType.Other"/>.</summary>
    Task<PendingProfileResponse?> GetPendingOtherProfileAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken = default);
}
