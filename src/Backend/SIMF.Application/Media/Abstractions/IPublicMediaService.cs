using SIMF.Contracts.Media;

namespace SIMF.Application.Media.Abstractions;

/// <summary>D-199 — public (anonymous) read of the active media gallery
/// (Mockup page 30). Paged; optionally narrowed to a single album. Mirrors
/// <c>IPublicDelegationService</c> but with paging because a gallery can be
/// large.</summary>
public interface IPublicMediaService
{
    Task<PublicMediaPage> ListAsync(
        string? album, int skip, int top,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches an item's primary image bytes (out-of-row, D-90),
    /// or null when the item is missing / inactive / has no stored image.</summary>
    Task<(byte[] Content, string ContentType)?> GetImageAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Fetches an item's thumbnail / poster bytes, or null.</summary>
    Task<(byte[] Content, string ContentType)?> GetThumbnailAsync(
        Guid id, CancellationToken cancellationToken = default);
}
