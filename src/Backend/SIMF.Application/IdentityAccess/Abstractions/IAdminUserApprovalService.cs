using SIMF.Contracts.Authentication;

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
}
