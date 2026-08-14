namespace SIMF.Contracts.Cms;

/// <summary>Public payload for one
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
    // Always null since the banner image became a StoredFile. The key stays on
    // the wire because the shipped app decodes it (D-219 append-only), but the
    // app's primary path is /app/assets/Banner/{id}/image, which serves an
    // upload and 302s an external link alike — so the fallback this fed has
    // nothing left to do.
    string? ImageUrl,
    string? LinkUrl,
    DateTime Start,
    DateTime End,
    int DisplayOrder);

/// <summary>List-of-banners response.</summary>
public sealed record PublicBanners(IReadOnlyList<PublicBanner> Items);
