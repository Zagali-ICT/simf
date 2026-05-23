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
    /// Creates a new Control Panel user account in the <c>Approved</c> state
    /// with no password, mints a 7-day password-set invitation code, and
    /// emails the new user an invitation link. Audited with actor + subject
    /// (D-042).
    /// </summary>
    Task<AdminCreateUserResponse> CreateUserAsync(
        Guid actorUserId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of accounts (decision D-044). Sortable on Email /
    /// DisplayName / AccountState / CreatedAt; filterable on the same set
    /// plus a free-text Search across email and display name. The endpoint
    /// clamps <c>Top</c> to a sensible ceiling.
    /// </summary>
    Task<GridPage<AdminUserSummary>> ListUsersAsync(
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
}
