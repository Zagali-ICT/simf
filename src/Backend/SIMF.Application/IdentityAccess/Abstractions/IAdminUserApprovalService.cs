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

    /// <summary>D-164 — bulk approve <see cref="UserType.Other"/>. Same
    /// shape as the visitor variant.</summary>
    Task<AdminBulkApprovalResponse> BulkApproveOthersAsync(
        Guid actorUserId,
        AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default);
}
