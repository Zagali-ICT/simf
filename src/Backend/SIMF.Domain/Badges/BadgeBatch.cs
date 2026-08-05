using SIMF.Domain.Common;

namespace SIMF.Domain.Badges;

/// <summary>
/// One bulk-badge generation run, kept so the placeholder badges minted together
/// can be managed as a unit afterwards — the QR pack re-emailed, or the whole
/// batch revoked. Each generated profile carries a back-reference to its row
/// here.
///
/// <para>Soft-deleted rather than removed, so those back-references always
/// resolve; revoking a batch deactivates it.</para>
/// </summary>
public class BadgeBatch : BaseAuditEntity
{
    /// <summary>A readable breakdown of what the batch minted, such as
    /// "VIP × 3 + Normal × 2", rendered as-is in the batches list so an admin
    /// recognises the run without expanding it. Built with the invariant culture,
    /// because Arabic-Indic digits or a Hijri date would drift once stored.</summary>
    public string CountsSummary { get; set; } = string.Empty;

    /// <summary>Denormalised from the member rows so the list does not count the
    /// profiles table once per row.</summary>
    public int TotalCount { get; set; }

    /// <summary>True when every badge in the batch was flagged as a delegation
    /// member.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>The organiser address the QR pack went to at generate time, which
    /// a re-email pre-fills. Null when the batch was generated without an
    /// email.</summary>
    public string? RecipientEmail { get; set; }
}
