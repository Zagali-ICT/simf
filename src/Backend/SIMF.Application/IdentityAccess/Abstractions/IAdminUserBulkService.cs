using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

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

    // ---------------------------------------------------------------------
    // D-113 — type-scoped bulk operations for the Visitor / Other grids.
    // The /admin/admins/* surface above stays untouched; these new methods
    // power /admin/visitors/* and /admin/others/*. The <paramref name="kind"/>
    // filter is the type-smuggling guard — any subject whose
    // <see cref="UserType"/> doesn't match is silently skipped per target.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Like <see cref="BulkDeleteUsersAsync"/> but narrowed to subjects whose
    /// <see cref="UserType"/> matches <paramref name="kind"/>. Subjects of
    /// the wrong UserType are counted as Skipped and audited as a delete
    /// failure with the AdminUserNotFound code — same shape as the unknown-id
    /// branch so an admin probing the wrong endpoint learns nothing.
    /// </summary>
    Task<AdminBulkDeleteResponse> BulkDeleteUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="DuplicateUserAsync"/> but refuses any source whose
    /// <see cref="UserType"/> doesn't match <paramref name="kind"/> — returns
    /// 404 (same code as a missing source) so cross-type duplication probes
    /// don't reveal whether a wrong-type id exists.
    /// </summary>
    Task<AdminCreateUserResponse> DuplicateUserByKindAsync(
        Guid actorUserId,
        UserType kind,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="ExportUsersAsync"/> but narrowed to subjects whose
    /// <see cref="UserType"/> matches <paramref name="kind"/> — both the
    /// selected-ids path and the whole-result-set path apply the filter
    /// BEFORE projection so a smuggled wrong-type id never appears in the
    /// workbook.
    /// </summary>
    Task<byte[]> ExportUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="ImportUsersAsync"/> but every created user is forced
    /// to <see cref="UserType"/> = <paramref name="kind"/> — any Role column
    /// in the spreadsheet is ignored for non-Admin kinds (the type-smuggling
    /// guard). For <see cref="UserType.Other"/> the optional ProfileTypeId
    /// column is mandatory per row; rows missing it land in the error report.
    /// </summary>
    Task<AdminImportUsersResponse> ImportUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        byte[] xlsx,
        CancellationToken cancellationToken = default);
}
