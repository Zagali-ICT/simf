namespace SIMF.Contracts.Admin;

// B9b — D-226: session-category dynamic lookup (FDS-004 §5.4). Admin CRUD
// contracts; mirrors the Organisation lookup shape (minus import). The CP
// session form loads the active rows via the list endpoint to populate the
// category picker, resolving the name client-side like the Hall/Company picker.

/// <summary>One session-category row in the admin grid.</summary>
public sealed record AdminSessionCategorySummary(
    Guid Id,
    string Name,
    string NameArabic,
    int DisplayOrder,
    bool IsActive);

/// <summary>Full session-category detail for the admin view/edit form.</summary>
public sealed record AdminSessionCategoryDetail(
    Guid Id,
    string Name,
    string NameArabic,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Admin create payload.</summary>
public sealed class AdminCreateSessionCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>Admin update payload.</summary>
public sealed class AdminUpdateSessionCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
