using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One programme theme / pillar (SIMF-FDS-004 §5.1). Themes are the
/// top-level grouping the agenda uses — sessions belong to themes; the
/// CP picker on the Session editor shows the active themes.
///
/// <para>Sprint B of D-134 introduces this entity under D-135's
/// freeze-lift. <see cref="Code"/> is the stable short identifier the
/// programme team uses in print materials (e.g. "DEF", "TECH"); the
/// bilingual Name pair is what visitors see.</para>
/// </summary>
public class Theme : BaseAuditEntity
{
    /// <summary>Short stable code for cross-reference (e.g. "DEF", "TECH").
    /// Unique, 2–16 chars. Programme team's choice — printable.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English display name (1–128 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Optional English description (≤ 1024 chars).</summary>
    public string? Description { get; set; }

    /// <summary>Optional Arabic description (≤ 1024 chars).</summary>
    public string? DescriptionArabic { get; set; }

    /// <summary>Sort key in the visitor-facing agenda + the CP picker
    /// (≥ 0; ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>The theme's accent colour. Free text (hex, CSS variable,
    /// etc.) — mirrors the D-115 ProfileType.PageColor contract.</summary>
    public string PageColor { get; set; } = string.Empty;
}
