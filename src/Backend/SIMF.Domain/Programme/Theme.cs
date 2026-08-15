using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>A programme theme, the top-level grouping the agenda uses.</summary>
public class Theme : BaseAuditEntity
{
    /// <summary>"DEF", "TECH"; also used in print. Minimum 2 characters, enforced
    /// in the admin service.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Accent colour, free text so it can be a hex value or a CSS
    /// variable.</summary>
    public string PageColor { get; set; } = string.Empty;
}
