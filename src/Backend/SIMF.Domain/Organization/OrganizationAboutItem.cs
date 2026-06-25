using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// D-495 — one "about" item (a bilingual title + text), e.g. the forum's
/// mission or vision. Ordered within the owning <see cref="OrganizationProfile"/>
/// and rendered in the app as a vision/mission card. Bilingual, orderable,
/// soft-deletable — mirrors the FAQ entry shape.
/// </summary>
public sealed class OrganizationAboutItem : BaseAuditEntity
{
    /// <summary>Owning profile (real DB FK — same DbContext).</summary>
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    /// <summary>English item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Arabic item title — paired with <see cref="Title"/>.</summary>
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>English item body text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Arabic item body text — paired with <see cref="Text"/>.</summary>
    public string TextArabic { get; set; } = string.Empty;

    /// <summary>Sort key within the owning profile (ascending).</summary>
    public int DisplayOrder { get; set; }
}
