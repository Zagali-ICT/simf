using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// D-495 — one "detail" row (a bilingual label + a value), e.g.
/// "Year : 2026" or "Location : Riyadh". Ordered within the owning
/// <see cref="OrganizationProfile"/> and rendered in the app as a
/// "name : value" list. Bilingual label + optional Arabic value,
/// orderable, soft-deletable.
/// </summary>
public sealed class OrganizationDetail : BaseAuditEntity
{
    /// <summary>Owning profile (real DB FK — same DbContext).</summary>
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    /// <summary>English label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic label — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The value in the default (English) reading, e.g. a year, a URL,
    /// or an organiser name.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The Arabic value — paired with <see cref="Value"/>. Optional: a
    /// language-neutral value (a year, a URL) leaves this blank and the app falls
    /// back to <see cref="Value"/>; a language-specific value (an organiser name)
    /// sets it so the Arabic reader sees Arabic.</summary>
    public string? ValueArabic { get; set; }

    /// <summary>Sort key within the owning profile (ascending).</summary>
    public int DisplayOrder { get; set; }
}
