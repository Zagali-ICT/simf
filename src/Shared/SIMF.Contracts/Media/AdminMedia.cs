using SIMF.Common.Enums;

namespace SIMF.Contracts.Media;

/// <summary>One row in the admin Media grid (Mockup page 30).</summary>
public sealed record AdminMediaSummary(
    Guid Id,
    MediaKind Kind,
    string? Title,
    string? TitleArabic,
    string? Album,
    string? AlbumArabic,
    bool HasImage,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>Full media-item detail (Details + Edit modals).
/// <c>HasImage</c> / <c>HasThumbnail</c> tell the CP whether the
/// out-of-row image endpoints will return bytes; the raw relative paths
/// are server-internal and intentionally not exposed.</summary>
public sealed record AdminMediaDetail(
    Guid Id,
    MediaKind Kind,
    string? Title,
    string? TitleArabic,
    string? Album,
    string? AlbumArabic,
    bool HasImage,
    bool HasThumbnail,
    string? Url,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Create payload. Bytes are uploaded separately via
/// <c>POST /admin/media/{id}/image</c> (out-of-row, D-90), so this carries
/// only metadata + the optional external <c>Url</c>.</summary>
public sealed class AdminCreateMediaRequest
{
    public MediaKind Kind { get; set; }
    public string? Title { get; set; }
    public string? TitleArabic { get; set; }
    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Update payload (metadata + active flag). Image bytes are
/// managed through the separate upload endpoint.</summary>
public class AdminUpdateMediaRequest
{
    public MediaKind Kind { get; set; }
    public string? Title { get; set; }
    public string? TitleArabic { get; set; }
    public string? Album { get; set; }
    public string? AlbumArabic { get; set; }
    public string? Url { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
