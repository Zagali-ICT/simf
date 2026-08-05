using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// A visitor captured as a lead by scanning their entry-badge QR at a booth.
/// Both user references are bare Guids: the users live in the Identity database,
/// so there is no navigation, no foreign key and no cross-database join.
///
/// <para>None of the visitor's own details are copied here. The full card is
/// resolved on read from their profile, so a lead list never goes stale against
/// the live record. Capture is idempotent per exhibiting user and visitor — a
/// repeat scan just refreshes the note and timestamp — and removal is a
/// soft-delete. The scan time is the inherited creation stamp.</para>
/// </summary>
public sealed class ExhibitorVisitorScan : BaseAuditEntity
{
    /// <summary>The exhibiting user who scanned.</summary>
    public Guid ExhibitorUserId { get; set; }

    /// <summary>The booth the capture belongs to, a real foreign key since both
    /// tables live in this database.
    ///
    /// <para>A lead belongs to the exhibiting company, not to one officer.
    /// Without this column two officers of the same booth kept disjoint lead
    /// lists and neither could see the other's captures. It is nullable only
    /// because it was added over rows that already existed: the migration
    /// backfills it from the capturing user's active membership, and a row whose
    /// capturer has no membership to resolve stays null and remains visible to
    /// that capturer alone.</para></summary>
    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>The captured visitor.</summary>
    public Guid VisitorUserId { get; set; }

    /// <summary>A free-text note the exhibitor attached.</summary>
    public string? Note { get; set; }
}
