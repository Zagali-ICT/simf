using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Admin-driven account-management use cases. The shape is the three
/// <see cref="UserType"/> families (Admin / Other / Visitor) per the
/// P7 model (decision D-048) — every method is named after its target
/// family; the implementation routes a shared <c>CreateAccount</c> /
/// <c>Approve</c> / <c>Reject</c> / <c>List</c> helper through the
/// right UserType filter.
/// </summary>
public interface IAdminAccountService
{
    // -- 2FA reset (D-041) ---------------------------------------------------

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

    // -- Admin family (UserType = Admin) -------------------------------------

    /// <summary>Creates a new Admin (P7c — replaces <c>CreateStaffAsync</c>).</summary>
    Task<AdminCreateUserResponse> CreateAdminAsync(
        Guid actorUserId,
        AdminCreateAdminRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Admin-typed accounts (P7c — replaces <c>ListStaffAsync</c>).</summary>
    Task<GridPage<AdminUserSummary>> ListAdminsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

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

    /// <summary>One page of pending-approval Admins (P7c).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingAdminsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Other family (UserType = Other) -------------------------------------

    /// <summary>Creates a new Other (P7c — new).</summary>
    Task<AdminCreateUserResponse> CreateOtherAsync(
        Guid actorUserId,
        AdminCreateOtherRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Other-typed accounts (P7c — new).</summary>
    Task<GridPage<AdminUserSummary>> ListOthersAsync(
        GridQuery query,
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

    /// <summary>One page of pending-approval Others (P7c — new).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingOthersAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- Visitor family (UserType = Visitor) ---------------------------------

    /// <summary>Creates a new Visitor (P3 — P7c added optional ProfileTypeId).</summary>
    Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One page of Visitor-typed accounts (P3 — P7c rekeyed off UserType).</summary>
    Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query,
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

    /// <summary>One page of pending-approval Visitors (P4).</summary>
    Task<GridPage<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);

    // -- ProfileTypes picker (P7c) -------------------------------------------

    /// <summary>
    /// Returns every active <c>ProfileType</c> row for the given
    /// <paramref name="userType"/>. Used by the CP create page to
    /// populate the subtype dropdown. P7c (decision D-048).
    /// </summary>
    Task<IReadOnlyList<AdminProfileTypeSummary>> ListProfileTypesAsync(
        UserType userType,
        CancellationToken cancellationToken = default);

    // -- Bulk admin operations (D-044 b — Admin family only today) -----------

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
    /// same UserType + RBAC roles, no password, fresh 7-day invite email
    /// (D-044 b).
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
    /// Bulk-creates Admin users from the rows in an XLSX workbook (D-044 b).
    /// Duplicate-email rows are skipped with a per-row error; every other
    /// failure is reported in the response. Each newly-created user gets a
    /// 7-day invite email exactly like the single-add flow.
    /// </summary>
    Task<AdminImportUsersResponse> ImportUsersAsync(
        Guid actorUserId,
        byte[] xlsx,
        CancellationToken cancellationToken = default);
}
