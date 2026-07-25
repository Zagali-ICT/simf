namespace SIMF.Contracts.Admin;

/// <summary>D-173 (gap doc G8) — admin grid row over ContentBlocks.</summary>
public sealed record AdminContentBlockSummary(
    Guid Id,
    string Key,
    string Content,
    string ContentArabic,
    bool IsActive,
    DateTimeOffset LastUpdatedAt,
    Guid LastUpdatedByUserId);

/// <summary>D-173 — upsert request. <see cref="Key"/> identifies the
/// row; the row is created if not present, updated in place if so.</summary>
public sealed class UpsertContentBlockRequest
{
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentArabic { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>D-173 — admin grid row over Banners.</summary>
public sealed record AdminBannerSummary(
    Guid Id,
    string Title,
    string TitleArabic,
    DateTimeOffset Start,
    DateTimeOffset End,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    // D-506 — carried so the grid Excel export can round-trip them (not rendered
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
    DateTimeOffset Start,
    DateTimeOffset End,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class CreateBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>D-173 — open for inheritance so the route-binding endpoint
/// can carry an <c>Id</c> field (matches the D-168 pattern).</summary>
public class UpdateBannerRequest
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
