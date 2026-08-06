namespace SIMF.Contracts.Cms;

/// <summary>D-173 (gap doc G8, PDF §1) — public payload for one
/// content block. Served by <c>GET /api/v1/app/content/{key}</c>.</summary>
public sealed record PublicContentBlock(
    string Key,
    string Content,
    string ContentArabic,
    DateTime LastUpdatedAt);

/// <summary>Batch read response.</summary>
public sealed record PublicContentBlockBatch(
    IReadOnlyDictionary<string, PublicContentBlock> Blocks);

/// <summary>Batch read request body.</summary>
public sealed class PublicContentBlockBatchRequest
{
    public IList<string> Keys { get; set; } = new List<string>();
}

/// <summary>Public banner payload (anonymous read).</summary>
public sealed record PublicBanner(
    Guid Id,
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string? ImageUrl,
    string? LinkUrl,
    DateTime Start,
    DateTime End,
    int DisplayOrder);

/// <summary>List-of-banners response.</summary>
public sealed record PublicBanners(IReadOnlyList<PublicBanner> Items);
