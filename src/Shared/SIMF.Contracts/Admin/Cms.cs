namespace SIMF.Contracts.Admin;

/// <summary>D-173 (gap doc G8) — admin grid row over ContentBlocks.</summary>
public sealed record AdminContentBlockSummary(
    Guid Id,
    string Key,
    string ContentEn,
    string ContentAr,
    bool IsActive,
    DateTimeOffset LastUpdatedAt,
    Guid LastUpdatedByUserId);

/// <summary>D-173 — upsert request. <see cref="Key"/> identifies the
/// row; the row is created if not present, updated in place if so.</summary>
public sealed class UpsertContentBlockRequest
{
    public string Key { get; set; } = string.Empty;
    public string ContentEn { get; set; } = string.Empty;
    public string ContentAr { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>D-173 — admin grid row over Banners.</summary>
public sealed record AdminBannerSummary(
    Guid Id,
    string TitleEn,
    string TitleAr,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record AdminBannerDetail(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string BodyEn,
    string BodyAr,
    string? ImageUrl,
    string? LinkUrl,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class CreateBannerRequest
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>D-173 — open for inheritance so the route-binding endpoint
/// can carry an <c>Id</c> field (matches the D-168 pattern).</summary>
public class UpdateBannerRequest
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
