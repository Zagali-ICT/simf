using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// A programme theme, the top-level grouping the agenda uses. Sessions belong to
/// themes, and the Control Panel's session editor offers the active ones.
/// </summary>
public class Theme : BaseAuditEntity
{
    /// <summary>A short stable code the programme team also uses in print, such
    /// as "DEF" or "TECH". Unique, 2 to 16 characters.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What visitors see, paired with <see cref="NameArabic"/>.</summary>
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    /// <summary>Ascending sort key in the agenda and the Control Panel
    /// picker.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>The theme's accent colour, free text so it can be a hex value or
    /// a CSS variable. The same contract profile types use for their own
    /// colour.</summary>
    public string PageColor { get; set; } = string.Empty;
}
