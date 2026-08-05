using SIMF.Domain.Common;

namespace SIMF.Domain.Regions;

/// <summary>
/// One administrative region in the bilingual regions lookup (e.g. the 13
/// official Saudi regions). A small, stable reference table the app reads to
/// populate region pickers. Bilingual like <c>Organisation</c> (NameArabic /
/// Name), soft-deleted via <see cref="BaseAuditEntity.IsActive"/>, and audited through the
/// row-audit trail by the admin service.
///
/// <para><see cref="Code"/> is the stable lookup key (e.g. "riyadh"). Unique
/// across regions so a re-seed updates / matches the existing row rather than
/// inserting a duplicate.</para>
/// </summary>
public class Region : BaseAuditEntity
{
    /// <summary>Stable lookup key / code (e.g. "riyadh") (1–16 chars). Required —
    /// unique across regions; the seed source keys on it.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Arabic region name (1–256 chars). Required — the primary display
    /// name in the bilingual lookup.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>English region name (≤ 256 chars). Optional.</summary>
    public string? Name { get; set; }

    /// <summary>Display order for the picker (ascending). Lower sorts first.</summary>
    public int SortOrder { get; set; }
}
