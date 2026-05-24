using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Admin-driven account-management use cases (<c>myComment</c> #33). First
/// slice — 2FA reset (D-041), create CP user + list users (D-042).
/// </summary>
public interface IAdminAccountService
{
    /// <summary>
    /// Wipes the target user's authenticator key + recovery codes + flips
    /// <c>TwoFactorEnabled</c> off, rolls the security stamp and revokes
    /// every refresh token. Audited with both actor and subject ids and a
    /// mandatory free-text reason (D-041).
    /// </summary>
    Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Control Panel **staff** user (P3 renamed from
    /// <c>CreateUserAsync</c>). Lands in <c>Approved</c> state with no
    /// password and a 7-day invitation code. Audited (D-042).
    /// </summary>
    Task<AdminCreateUserResponse> CreateStaffAsync(
        Guid actorUserId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new **visitor** account (P3). Same invitation-code shape
    /// as staff, but with no role grant and visitor-shape contract.
    /// Approval workflow + QR-mint-at-approval land in P4.
    /// </summary>
    Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of staff accounts (those that hold a CP role
    /// — Administrator today; Staff/Scientific/Security from P4). P3
    /// renamed from <c>ListUsersAsync</c>.
    /// </summary>
    Task<GridPage<AdminUserSummary>> ListStaffAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one page of visitor accounts — those without any CP role (P3).</summary>
    Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes one or many users by setting <c>AccountState = Disabled</c>,
    /// revoking refresh tokens and rolling the security stamp (D-044 b).
    /// Self-delete and Administrator-vs-Administrator deletes are rejected
    /// silently per target (counted as <c>Skipped</c>) — the batch does not
    /// fail. One audit row per subject so SOC sees every deletion.
    /// </summary>
    Task<AdminBulkDeleteResponse> BulkDeleteUsersAsync(
        Guid actorUserId,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user as a copy of an existing one — same display name,
    /// same Administrator-role membership, no password, fresh 7-day invite
    /// email (D-044 b).
    /// </summary>
    Task<AdminCreateUserResponse> DuplicateUserAsync(
        Guid actorUserId,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the bytes of an XLSX workbook with the selected users — when
    /// <c>Ids</c> is empty the export takes every user matching the
    /// (optional) <see cref="GridQuery"/>. Audited (D-044 b).
    /// </summary>
    Task<byte[]> ExportUsersAsync(
        Guid actorUserId,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-creates users from the rows in an XLSX workbook (D-044 b).
    /// Duplicate-email rows are skipped with a per-row error; every other
    /// failure is reported in the response. Each newly-created user gets a
    /// 7-day invite email exactly like the single-add flow.
    /// </summary>
    Task<AdminImportUsersResponse> ImportUsersAsync(
        Guid actorUserId,
        byte[] xlsx,
        CancellationToken cancellationToken = default);

    // -- P4 — approval workflow ------------------------------------------------

    /// <summary>
    /// Approves a pending staff account (P4). Flips <c>AccountState</c> to
    /// <c>Approved</c>, mints the QR id (D-046) and audits. Administrator-
    /// only at the endpoint; the service trusts the policy and only checks
    /// the target's current state.
    /// </summary>
    Task ApproveStaffAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending staff account (P4). Sets state to
    /// <c>Rejected</c>; audits with the actor + subject ids and the reason.
    /// </summary>
    Task RejectStaffAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending visitor (P4). Any CP role may call this — Staff,
    /// Scientific, Security or Administrator. Flips state and mints the QR.
    /// </summary>
    Task ApproveVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Rejects a pending visitor (P4). Any CP role.</summary>
    Task RejectVisitorAsync(
        Guid actorUserId,
        Guid subjectUserId,
        AdminRejectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one page of pending-approval staff (P4).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingStaffAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one page of pending-approval visitors (P4).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);
}
