namespace SIMF.Contracts.Cms;

/// <summary>D-173 (gap doc G8, PDF §1) — public payload for one
/// content block. Served by <c>GET /api/v1/content/{key}</c>.</summary>
public sealed record PublicContentBlock(
    string Key,
    string ContentEn,
    string ContentAr,
    DateTimeOffset LastUpdatedAt);

/// <summary>D-173 — batch read response.</summary>
public sealed record PublicContentBlockBatch(
    IReadOnlyDictionary<string, PublicContentBlock> Blocks);

/// <summary>D-173 — batch read request body.</summary>
public sealed class PublicContentBlockBatchRequest
{
    public IList<string> Keys { get; set; } = new List<string>();
}

/// <summary>D-173 — public banner payload (anonymous read).</summary>
public sealed record PublicBanner(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string BodyEn,
    string BodyAr,
    string? ImageUrl,
    string? LinkUrl,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int DisplayOrder);

/// <summary>D-173 — list-of-banners response.</summary>
public sealed record PublicBanners(IReadOnlyList<PublicBanner> Items);
