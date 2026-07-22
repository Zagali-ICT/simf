using SIMF.Common;
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
    /// <see cref="UserType"/> matches <paramref name="kind"/>. D-186:
    /// <paramref name="requirePartnerScope"/> further narrows the Visitor
    /// scope — <c>true</c> means "partner only" (linked ProfileType.IsVisitor
    /// = false), <c>false</c> means "audience only" (no ProfileType or
    /// IsVisitor = true), <c>null</c> means "no scope guard". Subjects of
    /// the wrong UserType (or wrong scope) are counted as Skipped and audited
    /// as a delete failure with the AdminUserNotFound code — same shape as
    /// the unknown-id branch so an admin probing the wrong endpoint learns
    /// nothing.
    /// </summary>
    Task<AdminBulkDeleteResponse> BulkDeleteUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="DuplicateUserAsync"/> but refuses any source whose
    /// <see cref="UserType"/> doesn't match <paramref name="kind"/> — returns
    /// 404 (same code as a missing source) so cross-type duplication probes
    /// don't reveal whether a wrong-type id exists. D-186:
    /// <paramref name="requirePartnerScope"/> applies the same scope guard
    /// as the bulk delete (see above).
    /// </summary>
    Task<AdminCreateUserResponse> DuplicateUserByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="ExportUsersAsync"/> but narrowed to subjects whose
    /// <see cref="UserType"/> matches <paramref name="kind"/> — both the
    /// selected-ids path and the whole-result-set path apply the filter
    /// BEFORE projection so a smuggled wrong-type id never appears in the
    /// workbook. D-186: <paramref name="requirePartnerScope"/> applies the
    /// same scope guard as the bulk delete (see above).
    /// </summary>
    Task<byte[]> ExportUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="ImportUsersAsync"/> but every created user is forced
    /// to <see cref="UserType.Visitor"/> (D-186 removed the standalone
    /// Other type). <paramref name="partnerScope"/>: <c>false</c> = audience
    /// import (ProfileTypeId optional, audience-side ProfileType required
    /// when supplied); <c>true</c> = partner import (ProfileTypeId mandatory
    /// per row and the chosen ProfileType must be IsVisitor=false).
    /// </summary>
    Task<AdminImportUsersResponse> ImportUsersByKindAsync(
        Guid actorUserId,
        bool partnerScope,
        byte[] xlsx,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-473 (#10) — bulk-generate placeholder badges by profile type + count
    /// (e.g. 10 VIP + 500 Normal). Each badge is an Approved visitor with default
    /// data (no real personal details) and a freshly minted QR; when
    /// <see cref="AdminBulkGenerateBadgesRequest.IsDelegate"/> is set the badges
    /// carry the delegation (وفد) flag. Bounded per request; throws 400 on an
    /// invalid profile type / count.
    /// </summary>
    Task<AdminBulkGenerateBadgesResponse> BulkGenerateBadgesAsync(
        Guid actorUserId,
        UserType kind,
        AdminBulkGenerateBadgesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-758 (#10 Phase 2) — the server-paged list of persisted bulk-badge batches
    /// (<see cref="BulkGenerateBadgesAsync"/> runs), newest first. A revoked batch
    /// stays in the list with <c>IsActive = false</c>.
    /// </summary>
    Task<GridPage<AdminBadgeBatchSummary>> ListBadgeBatchesAsync(
        Guid actorUserId,
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-758 (#10 Phase 2) — re-render the batch's QR pack and email it to the
    /// supplied organiser address (a fresh copy; the badges are unchanged). Throws
    /// 404 for an unknown batch, 400 for an invalid email or an empty batch.
    /// </summary>
    Task<AdminReEmailBadgeBatchResponse> ReEmailBadgeBatchAsync(
        Guid actorUserId,
        AdminReEmailBadgeBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-758 (#10 Phase 2) — revoke a batch: disable every account it minted
    /// (reusing the type-scoped bulk-delete path) and mark the batch inactive.
    /// Throws 404 for an unknown / already-revoked batch. Not a cross-DB
    /// transaction (D-157): the member accounts (Identity DB) are disabled first,
    /// then the batch (App DB) is deactivated as a separate unit of work.
    /// </summary>
    Task<AdminRevokeBadgeBatchResponse> RevokeBadgeBatchAsync(
        Guid actorUserId,
        AdminRevokeBadgeBatchRequest request,
        CancellationToken cancellationToken = default);
}
