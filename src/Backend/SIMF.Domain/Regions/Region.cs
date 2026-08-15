using SIMF.Domain.Common;

namespace SIMF.Domain.Regions;

/// <summary>
/// One administrative region in the bilingual regions lookup (the 13 official Saudi
/// regions), read by the app to populate region pickers.
/// </summary>
public class Region : BaseAuditEntity
{
    /// <summary>The stable lookup key, e.g. "riyadh"; the seeder matches on it.</summary>
    public string Code { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public string? Name { get; set; }

    public int SortOrder { get; set; }
}
