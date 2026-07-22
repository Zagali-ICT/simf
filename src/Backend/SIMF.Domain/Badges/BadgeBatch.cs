using SIMF.Domain.Common;

namespace SIMF.Domain.Badges;

/// <summary>
/// D-758 (#10 Phase 2) — one bulk-badge generation run, persisted so the set of
/// placeholder badges minted together can be managed as a unit afterwards
/// (re-email the QR pack, or revoke the whole batch). Before D-758 the "batch"
/// existed only in the <c>AdminBulkGenerateBadgesRequest</c> DTO and evaporated
/// once the loop finished; each generated <see cref="Profiles.UserProfile"/> now
/// carries a nullable <c>BadgeBatchId</c> back-reference to its row here.
///
/// <para>Lives entirely in <c>SIMF_App</c>: the FK from <c>UserProfile</c> is an
/// intra-database relation (both sides are App entities), so the D-157
/// Data/Identity separation rule does not apply — this row holds no
/// Identity-owned data. Soft-deleted via <see cref="BaseAuditEntity.IsActive"/>
/// (revoke calls <see cref="BaseAuditEntity.Deactivate"/>); never hard-deleted,
/// so the member profiles' back-reference always resolves.</para>
/// </summary>
public class BadgeBatch : BaseAuditEntity
{
    /// <summary>Human-readable breakdown of what the batch minted, e.g.
    /// <c>"VIP × 3 + Normal × 2"</c>. Rendered as-is in the CP batches list so an
    /// admin recognises the run without expanding it. Built invariant-culture at
    /// generate time (Arabic-Indic digits / Hijri would drift a stored string).</summary>
    public string CountsSummary { get; set; } = string.Empty;

    /// <summary>Total number of badges minted in this batch — denormalised from the
    /// member rows so the list does not COUNT the profiles table per row.</summary>
    public int TotalCount { get; set; }

    /// <summary>True when every badge in the batch was flagged as a delegation
    /// member (وفد) — mirrors <c>AdminBulkGenerateBadgesRequest.IsDelegate</c>.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>The organiser address the QR pack was emailed to at generate time,
    /// if any (the default recipient a re-email pre-fills). Null when the batch was
    /// generated without an email.</summary>
    public string? RecipientEmail { get; set; }
}
