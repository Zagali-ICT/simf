using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Admin-driven bulk operations on the user collection (D-044 b):
/// bulk-delete, XLSX export, XLSX import. R2 — D-075: split out of
/// <c>IAdminAccountService</c> per Architecture SEV-1.2.
/// </summary>
public interface IAdminUserBulkService
{
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
    /// Returns the bytes of an XLSX workbook with the selected users — when
    /// <c>Ids</c> is empty the export takes every user matching the
    /// (optional) <see cref="SIMF.Common.GridQuery"/>. Audited (D-044 b).
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
