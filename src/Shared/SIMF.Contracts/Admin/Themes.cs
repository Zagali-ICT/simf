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
    DateTime CreatedAt,
    // Carry the bilingual descriptions so the Excel export can surface
    // them (optional/defaulted so existing positional callers are unaffected).
    string? Description = null,
    string? DescriptionArabic = null);

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
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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
/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class (D-505 / D-844) so it cannot drop a field at bind time.</remarks>
public class AdminUpdateThemeRequest
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
