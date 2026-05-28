namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Themes grid (D-134 Sprint B —
/// SIMF-FDS-004 §5.1).</summary>
public sealed record AdminThemeSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    int DisplayOrder,
    string PageColor,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>One full theme record (for the Edit / Details modals; adds
/// the bilingual descriptions the grid summary omits).</summary>
public sealed record AdminThemeDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Description,
    string? DescriptionArabic,
    int DisplayOrder,
    string PageColor,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>POST body for <c>/admin/themes</c>.</summary>
public sealed class AdminCreateThemeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public int DisplayOrder { get; set; }
    public string PageColor { get; set; } = string.Empty;
}

/// <summary>PUT body for <c>/admin/themes/{id}</c>.</summary>
public sealed class AdminUpdateThemeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public int DisplayOrder { get; set; }
    public string PageColor { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
