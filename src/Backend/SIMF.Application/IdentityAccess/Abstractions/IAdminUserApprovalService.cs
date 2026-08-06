using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-driven approve / reject for the three admin queues (Admin / Other /
/// Visitor). Split out of <c>IAdminAccountService</c>.
/// </summary>
public interface IAdminUserApprovalService
{
    /// <summary>Approve a pending Admin.</summary>
    Task ApproveAdminAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Admin. Reason is mandatory.</summary>
    Task RejectAdminAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Approve a pending Other.</summary>
    Task ApproveOtherAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Other. Reason is mandatory.</summary>
    Task RejectOtherAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Approve a pending Visitor. An optional
    /// <paramref name="profileTypeId"/> sets the visitor's tier as part of
    /// the approval; null leaves it unchanged. A non-null id must be an
    /// active, audience-side (IsForVisitor=true) ProfileType — otherwise the
    /// call fails with <c>ADMIN_PROFILE_TYPE_INVALID</c> (400).</summary>
    Task ApproveVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        Guid? profileTypeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending Visitor. Reason is mandatory.</summary>
    Task RejectVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk approve a batch of pending <see cref="UserType.Visitor"/>
    /// users. Each subject is approved in its own atomic step; per-subject
    /// failures are recorded in the returned report and do not block the rest.
    /// One row-audit + one operation-log row per subject (the "Select All"
    /// affordance the security team needs).</summary>
    Task<AdminBulkApprovalResponse> BulkApproveVisitorsAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk approve partner-side accounts (Visitor users whose
    /// linked ProfileType.IsVisitor is false). Same shape as the visitor
    /// variant.</summary>
    Task<AdminBulkApprovalResponse> BulkApproveOthersAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk approve a batch of pending
    /// <see cref="UserType.Admin"/> (staff) accounts. The admin-queue
    /// counterpart of <see cref="BulkApproveVisitorsAsync"/>; same per-subject
    /// semantics (each via the single-approve path under the Admin scope).</summary>
    Task<AdminBulkApprovalResponse> BulkApproveAdminsAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk reject a batch of pending audience-side
    /// <see cref="UserType.Visitor"/> users with one shared reason. Mirrors
    /// <see cref="BulkApproveVisitorsAsync"/>: each subject is rejected in its
    /// own step via the single-reject path (scope guard + state flip + token
    /// revoke + audit + notification); per-subject failures are reported and
    /// do not block the rest.</summary>
    Task<AdminBulkRejectResponse> BulkRejectVisitorsAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk reject partner-side (Other) pending accounts
    /// (Visitor users whose linked ProfileType.IsVisitor is false). Same
    /// shape as the visitor variant.</summary>
    Task<AdminBulkRejectResponse> BulkRejectOthersAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk reject a batch of pending
    /// <see cref="UserType.Admin"/> (staff) accounts with one shared reason.
    /// The admin-queue counterpart of <see cref="BulkRejectVisitorsAsync"/>.</summary>
    Task<AdminBulkRejectResponse> BulkRejectAdminsAsync(
        Guid actorUserId,
        AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default);
}
