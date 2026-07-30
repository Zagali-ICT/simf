using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// D-426 — a visitor an exhibitor ("Other" profile type) captured by scanning
/// the visitor's entry-badge QR at their booth (lead capture). Both user
/// references are <b>bare Guid logical FKs</b> to <c>SimfUser.Id</c> on the
/// Identity DB — no EF navigation, no DB FK, no cross-DB join (D-157).
///
/// <para>No snapshot of the visitor's PII is stored (D-157 forbids extending the
/// audit-snapshot copy pattern to live data): the full visitor card is resolved
/// on read from the visitor's <c>UserProfile</c> plus a permitted email
/// round-trip. Capture is idempotent per
/// (<see cref="ExhibitorUserId"/>, <see cref="VisitorUserId"/>); a repeat scan
/// just refreshes the note / timestamp. Removing is a soft-delete
/// (<see cref="BaseAuditEntity.Deactivate"/>); <c>ScannedAt</c> is the inherited
/// <see cref="BaseAuditEntity.CreatedAt"/>.</para>
/// </summary>
public sealed class ExhibitorVisitorScan : BaseAuditEntity
{
    /// <summary>The exhibitor (Other) who scanned — bare Guid → <c>SimfUser.Id</c>.</summary>
    public Guid ExhibitorUserId { get; set; }

    /// <summary>FR-EXH-003 — the BOOTH the capture belongs to (real FK to
    /// <see cref="Exhibitor.Id"/>; both tables live on the App DB, so this one is
    /// a genuine constraint, unlike the two cross-DB user refs).
    ///
    /// <para>A lead is the exhibiting company's, not one officer's: without this
    /// column two officers of the same booth kept two disjoint lead lists and
    /// neither could see the other's captures. Nullable because the column is
    /// ADDITIVE over shipped rows — the migration backfills it from the capturing
    /// user's active <see cref="ExhibitorMembership"/>, and a row whose capturer
    /// has no membership to resolve stays null and remains visible to that
    /// capturer alone.</para></summary>
    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>The captured visitor — bare Guid → <c>SimfUser.Id</c>.</summary>
    public Guid VisitorUserId { get; set; }

    /// <summary>Optional free-text note the exhibitor attached (≤512 chars).</summary>
    public string? Note { get; set; }
}
