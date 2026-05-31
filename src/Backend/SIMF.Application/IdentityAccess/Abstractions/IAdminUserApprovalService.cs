using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-driven approve / reject for the three <c>UserType</c> families
/// (Admin / Other / Visitor) per P7 (D-048). R2 — D-075: split out of
/// <c>IAdminAccountService</c> per Architecture SEV-1.2.
/// </summary>
public interface IAdminUserApprovalService
{
    /// <summary>Approve a pending Admin (P7c).</summary>
    Task ApproveAdminAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Admin (P7c). Reason is mandatory.</summary>
    Task RejectAdminAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Approve a pending Other (P7c — new).</summary>
    Task ApproveOtherAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Other (P7c — new). Reason is mandatory.</summary>
    Task RejectOtherAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Approve a pending Visitor (P4).</summary>
    Task ApproveVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Visitor (P4). Reason is mandatory.</summary>
    Task RejectVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>D-164 (PDF §2.7.1, gap doc G2) — bulk approve a batch of
    /// pending <see cref="UserType.Visitor"/> users. Each subject is
    /// approved in its own atomic step; per-subject failures are recorded
    /// in the returned report and do not block the rest. One row-audit +
    /// one operation-log row per subject (the "Select All" affordance the
    /// security team needs).</summary>
    Task<AdminBulkApprovalResponse> BulkApproveVisitorsAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>D-164 — bulk approve partner-side accounts (D-186:
    /// Visitor users whose linked ProfileType.IsVisitor is false).
    /// Same shape as the visitor variant.</summary>
    Task<AdminBulkApprovalResponse> BulkApproveOthersAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>P1.3 (D-214) — bulk approve a batch of pending
    /// <see cref="UserType.Admin"/> (staff) accounts. The admin-queue
    /// counterpart of <see cref="BulkApproveVisitorsAsync"/>; same per-subject
    /// semantics (each via the single-approve path under the Admin scope).</summary>
    Task<AdminBulkApprovalResponse> BulkApproveAdminsAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>D-209 — bulk reject a batch of pending audience-side
    /// <see cref="UserType.Visitor"/> users with one shared reason. Mirrors
    /// <see cref="BulkApproveVisitorsAsync"/>: each subject is rejected in its
    /// own step via the single-reject path (scope guard + state flip + token
    /// revoke + audit + notification); per-subject failures are reported and
    /// do not block the rest.</summary>
    Task<AdminBulkRejectResponse> BulkRejectVisitorsAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>D-209 — bulk reject partner-side (Other) pending accounts
    /// (Visitor users whose linked ProfileType.IsVisitor is false). Same
    /// shape as the visitor variant.</summary>
    Task<AdminBulkRejectResponse> BulkRejectOthersAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>P1.3 (D-214) — bulk reject a batch of pending
    /// <see cref="UserType.Admin"/> (staff) accounts with one shared reason.
    /// The admin-queue counterpart of <see cref="BulkRejectVisitorsAsync"/>.</summary>
    Task<AdminBulkRejectResponse> BulkRejectAdminsAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);
}
