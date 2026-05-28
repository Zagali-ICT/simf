using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;

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

    // -- D-126 — Q-G reversal: admin can read any visitor or Other profile -----
    //
    // The walk-in registration desk (D-127) needs to display the full
    // registration for accounts in ANY state (Approved walk-ins, Rejected
    // accounts being investigated, …) — not just PendingApproval. Q-G
    // (locked 2026-05-26) blocked a general profile-read; this reversal
    // adds two scoped reads (per kind) that work for any state. The same
    // type-match guard from D-124 is preserved (a Visitor id on the /others
    // route still 404s) so admins can't enumerate cross-kind ids. Every
    // call writes a row-audit entry automatically via the D-109 SaveChanges
    // interceptor when the underlying SimfUser row materialises.

    /// <summary>D-126 — any-state visitor profile read. Returns null when
    /// the id is missing or the target is not a Visitor.</summary>
    Task<AdminUserProfileView?> GetVisitorProfileAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>D-126 — any-state Other profile read.</summary>
    Task<AdminUserProfileView?> GetOtherProfileAsync(
        Guid subjectUserId,
        CancellationToken cancellationToken = default);
}
