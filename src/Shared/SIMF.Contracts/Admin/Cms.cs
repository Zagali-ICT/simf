namespace SIMF.Contracts.Admin;

/// <summary>Admin grid row over ContentBlocks.</summary>
public sealed record AdminContentBlockSummary(
    Guid Id,
    string Key,
    string Content,
    string ContentArabic,
    bool IsActive,
    DateTime LastUpdatedAt,
    Guid LastUpdatedByUserId);

/// <summary>Upsert request. <see cref="Key"/> identifies the
/// row; the row is created if not present, updated in place if so.</summary>
public sealed class UpsertContentBlockRequest
{
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentArabic { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Admin grid row over Banners.</summary>
public sealed record AdminBannerSummary(
    Guid Id,
    string Title,
    string TitleArabic,
    DateTime Start,
    DateTime End,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    // Carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Body/BodyArabic are required for create; ImageUrl/LinkUrl
    // are optional. Default to empty/null when unset.
    string Body = "",
    string BodyArabic = "",
    string? ImageUrl = null,
    string? LinkUrl = null);

public sealed record AdminBannerDetail(
    Guid Id,
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string? ImageUrl,
    string? LinkUrl,
    DateTime Start,
    DateTime End,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Open for inheritance so the route-binding endpoint
/// can carry an <c>Id</c> field, matching the other admin requests.</summary>
public class UpdateBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
