using SIMF.Common.Enums;

namespace SIMF.Contracts.Assets;

/// <summary>D-357 — body for setting a unified media asset to an external link
/// (the route carries the category + owner id). Not sealed so the API can derive
/// a route-bound request from it (mirrors <c>UpdateMediaRoute</c>).</summary>
public class SetAssetLinkRequest
{
    public AssetKind Kind { get; set; } = AssetKind.Image;
    public string Url { get; set; } = string.Empty;
}

/// <summary>D-357 — one row in the central Media Library grid. <c>OwnerName</c> is
/// resolved per category (speaker / company / sponsor / partner name, archive year,
/// news title) so the page is readable without a second fetch.</summary>
public sealed record AdminAssetSummary(
    Guid Id,
    AssetCategory Category,
    Guid OwnerId,
    string? OwnerName,
    AssetKind Kind,
    AssetSourceType SourceType,
    string? ExternalUrl,
    string? ContentType,
    long? SizeBytes,
    string? OriginalFileName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
