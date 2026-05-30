using SIMF.Common.Enums;

namespace SIMF.Contracts.Media;

/// <summary>D-199 — one row in the admin Media grid (Mockup page 30).</summary>
public sealed record AdminMediaSummary(
    Guid Id,
    MediaKind Kind,
    string? TitleEn,
    string? TitleAr,
    string? AlbumEn,
    string? AlbumAr,
    bool HasImage,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-199 — full media-item detail (Details + Edit modals).
/// <c>HasImage</c> / <c>HasThumbnail</c> tell the CP whether the
/// out-of-row image endpoints will return bytes; the raw relative paths
/// are server-internal and intentionally not exposed.</summary>
public sealed record AdminMediaDetail(
    Guid Id,
    MediaKind Kind,
    string? TitleEn,
    string? TitleAr,
    string? AlbumEn,
    string? AlbumAr,
    bool HasImage,
    bool HasThumbnail,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-199 — create payload. Bytes are uploaded separately via
/// <c>POST /admin/media/{id}/image</c> (out-of-row, D-90), so this carries
/// only metadata + the optional external <c>Url</c>.</summary>
public sealed class AdminCreateMediaRequest
{
    public MediaKind Kind { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleAr { get; set; }
    public string? AlbumEn { get; set; }
    public string? AlbumAr { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>D-199 — update payload (metadata + active flag). Image bytes are
/// managed through the separate upload endpoint.</summary>
public class AdminUpdateMediaRequest
{
    public MediaKind Kind { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleAr { get; set; }
    public string? AlbumEn { get; set; }
    public string? AlbumAr { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
