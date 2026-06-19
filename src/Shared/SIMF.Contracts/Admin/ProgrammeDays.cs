namespace SIMF.Contracts.Admin;

// D-452 — programme-days admin CRUD (Figma 883:2308 "تفاصيل اليوم"). Mirrors the
// session-category lookup shape + a Date and a HasImage flag (the day's logo
// rides the unified D-357 asset pipeline — AssetCategory.ProgrammeDayImage owned
// by the day's Id; there is no logo column).

/// <summary>One programme-day row in the admin grid.</summary>
public sealed record AdminProgrammeDaySummary(
    Guid Id,
    DateOnly Date,
    string Title,
    string TitleArabic,
    int DisplayOrder,
    bool HasImage,
    bool IsActive);

/// <summary>Full programme-day detail for the admin view/edit form.</summary>
public sealed record AdminProgrammeDayDetail(
    Guid Id,
    DateOnly Date,
    string Title,
    string TitleArabic,
    int DisplayOrder,
    bool HasImage,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Admin create payload.</summary>
public sealed class AdminCreateProgrammeDayRequest
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>Admin update payload.</summary>
public sealed class AdminUpdateProgrammeDayRequest
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
