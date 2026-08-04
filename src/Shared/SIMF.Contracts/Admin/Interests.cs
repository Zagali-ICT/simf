namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin <c>Interests</c> grid (P9 — D-050).</summary>
public sealed record AdminInterestSummary(
    Guid Id,
    string Name,
    string NameArabic,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>The body of <c>POST /api/v1/admin/interests</c> (P9). The CP
/// page builds this when an admin clicks <i>Add interest</i>.</summary>
public sealed class AdminCreateInterestRequest
{
    /// <summary>English display name (1-128 chars; unique).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1-128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Sort key in the visitor picker (≥ 0).</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>The body of <c>PUT /api/v1/admin/interests/{id}</c> (P9). Same
/// fields as the create request plus the soft-delete flag.</summary>
/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class (D-505 / D-844) so it cannot drop a field at bind time.</remarks>
public class AdminUpdateInterestRequest
{
    /// <summary>English display name (1-128 chars; unique).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1-128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Sort key in the visitor picker (≥ 0).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Soft-delete flag; <c>false</c> deactivates the interest.</summary>
    public bool IsActive { get; set; } = true;
}
