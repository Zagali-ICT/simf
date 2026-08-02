namespace SIMF.Contracts.Regions;

// Region lookup — bilingual administrative-regions directory (the 13 official
// Saudi regions). Admin CRUD contracts + the public picker item the app reads.

/// <summary>One region row in the admin grid.</summary>
public sealed record AdminRegionSummary(
    Guid Id,
    string Code,
    string? Name,
    string NameArabic,
    bool IsActive);

/// <summary>Full region detail — every column for the admin view/edit form.</summary>
public sealed record AdminRegionDetail(
    Guid Id,
    string Code,
    string? Name,
    string NameArabic,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Admin create payload.</summary>
public sealed class CreateRegionRequest
{
    /// <summary>Stable lookup key / code (required, unique).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English region name (optional).</summary>
    public string? Name { get; set; }

    /// <summary>Arabic region name (required).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Display order for the picker (ascending).</summary>
    public int SortOrder { get; set; }
}

/// <summary>Admin update payload.</summary>
public class UpdateRegionRequest
{
    /// <summary>Stable lookup key / code (required, unique).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English region name (optional).</summary>
    public string? Name { get; set; }

    /// <summary>Arabic region name (required).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Display order for the picker (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>Whether the region is active.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Lightweight region item for the public app picker.</summary>
public sealed record RegionPickerItem(
    string Code,
    string? Name,
    string NameArabic);
